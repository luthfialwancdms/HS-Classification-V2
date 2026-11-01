using System.Reflection;
using System.Security.Claims;
using HsClassifier.API.Controllers;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HsClassifier.Tests;

/// <summary>The AI status endpoints are Admin-only, evaluated with the real ASP.NET authorization service.</summary>
public class AdminAiControllerGuardTests
{
    private static ClaimsPrincipal User(params string[] roles) => new(new ClaimsIdentity(
        roles.Select(r => new Claim(ClaimTypes.Role, r)).Append(new Claim(ClaimTypes.NameIdentifier, "1")), "test"));

    private static async Task<bool> Allowed(ClaimsPrincipal user)
    {
        using var sp = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(
            sp.GetRequiredService<IAuthorizationPolicyProvider>(),
            typeof(AdminAiController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        var result = await sp.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy!.Requirements);
        return result.Succeeded;
    }

    [Fact]
    public async Task Admin_passes() => Assert.True(await Allowed(User(nameof(UserRole.Admin))));

    [Theory]
    [InlineData(nameof(UserRole.Classifier))]
    [InlineData(nameof(UserRole.ExpertReviewer))]
    [InlineData(nameof(UserRole.Viewer))]
    public async Task Other_roles_are_refused(string role) => Assert.False(await Allowed(User(role)));

    [Fact]
    public async Task An_anonymous_caller_is_refused() =>
        Assert.False(await Allowed(new ClaimsPrincipal(new ClaimsIdentity())));

    [Fact]
    public void No_action_opts_out_of_the_guard()
    {
        var actions = typeof(AdminAiController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(actions);
        Assert.All(actions, m => Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>()));
        Assert.Null(typeof(AdminAiController).GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void The_check_is_a_post_so_a_refresh_or_prefetch_cannot_trigger_a_paid_request()
    {
        var check = typeof(AdminAiController).GetMethod(nameof(AdminAiController.Check))!;
        Assert.NotNull(check.GetCustomAttribute<HttpPostAttribute>());
        var status = typeof(AdminAiController).GetMethod(nameof(AdminAiController.GetStatus))!;
        Assert.NotNull(status.GetCustomAttribute<HttpGetAttribute>());
    }

    [Fact]
    public void Status_endpoint_never_calls_the_ai()
    {
        var provider = new FakeProvider();
        var sp = AiTest.Services(Guid.NewGuid().ToString(), more: s => s.AddScoped<IAiProvider>(_ => provider));
        var checker = new AiConnectionChecker(sp.GetRequiredService<IServiceScopeFactory>(), new CapturingRecorder(),
            TimeProvider.System, new ConfigurationBuilder().Build(), Microsoft.Extensions.Logging.Abstractions.NullLogger<AiConnectionChecker>.Instance);
        var controller = new AdminAiController(provider, checker,
            new AiUsageReportService(new HsClassifierDbContext(AiTest.NewOptions()), new ConfigurationBuilder().Build(), TimeProvider.System));

        var ok = Assert.IsType<OkObjectResult>(controller.GetStatus());
        var body = Assert.IsType<Dictionary<string, object?>>(ok.Value);

        Assert.Equal(0, provider.ProbeCalls);
        Assert.Equal(false, body["pernahDicek"]);
        Assert.Equal(true, body["keyTerkonfigurasi"]); // a boolean only, never the key
        Assert.DoesNotContain(body.Keys, k => k.Contains("key", StringComparison.OrdinalIgnoreCase) && k != "keyTerkonfigurasi");
    }

    [Theory]
    [InlineData(null, "openai-compatible")]
    [InlineData("groq", "openai-compatible")]
    [InlineData("gemini", "gemini")]
    public void Classification_gets_the_logging_wrapper_around_the_configured_provider(string? choice, string expectedProvider)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(
            choice is null ? [] : new Dictionary<string, string?> { ["Ai:Provider"] = choice }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(cfg);
        services.AddDbContext<HsClassifierDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddInfrastructure();
        using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = sp.CreateScope();

        var suggester = scope.ServiceProvider.GetRequiredService<ITermSuggester>();
        var provider = scope.ServiceProvider.GetRequiredService<IAiProvider>();

        Assert.IsType<LoggingTermSuggester>(suggester);
        Assert.Equal(expectedProvider, provider.ProviderName);
        Assert.Equal(provider.Model, suggester.Model);
        Assert.NotNull(sp.GetRequiredService<AiConnectionChecker>()); // singleton resolves without capturing scoped services
    }
}
