using HsClassifier.Application.Interfaces;
using HsClassifier.Infrastructure.Template;
using Microsoft.Extensions.Configuration;
using HsClassifier.Infrastructure.Parsing;
using HsClassifier.Infrastructure.Seeding;
using HsClassifier.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HsClassifier.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IVersionManager, VersionManager>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IDocxParser, DocxParser>();
        services.AddScoped<IBtkiPdfParser, BtkiPdfParser>();
        services.AddScoped<BtkiImportService>();
        services.AddScoped<CandidateGeneratorService>();
        services.AddScoped<ClassificationValidator>();
        services.AddScoped<TariffLookupService>();
        // AI term-fallback provider is config-selectable: Ai:Provider = groq (default) | gemini
        services.AddScoped<ITermSuggester>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return cfg["Ai:Provider"]?.Trim().ToLowerInvariant() == "gemini"
                ? ActivatorUtilities.CreateInstance<GeminiTermSuggester>(sp)
                : ActivatorUtilities.CreateInstance<GroqTermSuggester>(sp);
        });
        services.AddScoped<ClassificationOrchestrator>();
        services.AddScoped<ExpertReviewService>();
        services.AddScoped<BtkiCsvSeeder>();
        services.AddScoped<TemplateIngestionService>();
        return services;
    }
}


