using HsClassifier.Application.Interfaces;
using HsClassifier.Infrastructure.Template;
using Microsoft.Extensions.Configuration;
using HsClassifier.Infrastructure.Parsing;
using HsClassifier.Infrastructure.Seeding;
using HsClassifier.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddScoped<IAiProvider>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            return cfg["Ai:Provider"]?.Trim().ToLowerInvariant() == "gemini"
                ? ActivatorUtilities.CreateInstance<GeminiTermSuggester>(sp)
                : ActivatorUtilities.CreateInstance<GroqTermSuggester>(sp);
        });
        // Classification sees the same ITermSuggester as before; the wrapper only adds usage logging.
        services.AddScoped<ITermSuggester>(sp =>
            ActivatorUtilities.CreateInstance<LoggingTermSuggester>(sp, sp.GetRequiredService<IAiProvider>()));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAiUsageRecorder, AiUsageRecorder>();
        services.AddSingleton<AiConnectionChecker>();
        services.AddScoped<AiUsageReportService>();
        services.AddScoped<DailyQuotaService>();
        services.AddScoped<ExportService>();
        services.AddScoped<ClassificationOrchestrator>();
        services.AddScoped<ExpertReviewService>();
        services.AddScoped<BtkiCsvSeeder>();
        services.AddScoped<TemplateIngestionService>();
        return services;
    }
}


