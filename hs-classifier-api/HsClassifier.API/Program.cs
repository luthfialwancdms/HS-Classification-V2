using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/hs-classifier-.log", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// OpenAPI docs (Task 19.3) — /swagger in Development
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HS Classifier V2 API",
        Version = "v1",
        Description = "Klasifikasi HS berbasis database BTKI 2022 + KUM HS 1-6. " +
                      "Error body: { message } dengan status 400 (input/transisi tidak valid), 401/403 (auth), " +
                      "404 (case/versi tidak ada), 422 (template berisi error). " +
                      "Status klasifikasi: DETERMINED | NEED_CLARIFICATION | NEED_EXPERT_REVIEW | ERROR " +
                      "(ERROR = validasi gagal: CODE_NOT_FOUND, INVALID_FORMAT, PARENT_MISSING, DESCRIPTION_MISMATCH, INACTIVE_CODE, SOURCE_ID_INVALID)."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token dari POST /api/auth/login",
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "HsClassifier.API.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

// Database
builder.Services.AddDbContext<HsClassifierDbContext>(options =>
    options.UseHsClassifierDatabase(builder.Configuration));

// Infrastructure services
builder.Services.AddInfrastructure();

// CORS for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader());
});


// JWT Authentication — no hardcoded fallback key; set Jwt:Key via env var Jwt__Key or user-secrets
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key must be configured (min 32 bytes). Use env var Jwt__Key or dotnet user-secrets.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey        = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer          = true,
            ValidIssuer             = builder.Configuration["Jwt:Issuer"] ?? "HsClassifierAPI",
            ValidateAudience        = true,
            ValidAudience           = builder.Configuration["Jwt:Audience"] ?? "HsClassifierClient",
            ClockSkew               = TimeSpan.Zero,
        };
    });
// Secure by default: every endpoint requires a valid JWT unless marked [AllowAnonymous]
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build());
// Health checks
builder.Services.AddHealthChecks();

// Rate limits (per minute). "classify": per signed-in user; "auth": per IP, slows password guessing.
// Defaults in code; override with RateLimit:ClassifyPerMinute / RateLimit:LoginPerMinute.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Terlalu banyak permintaan. Tunggu sebentar lalu coba lagi." }, ct);
    };
    int Limit(string key, int fallback) => builder.Configuration.GetValue(key, fallback) is var n && n > 0 ? n : fallback;
    string Ip(HttpContext h) => h.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    o.AddPolicy("classify", h => RateLimitPartition.GetFixedWindowLimiter(
        h.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Ip(h),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit("RateLimit:ClassifyPerMinute", 20), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("auth", h => RateLimitPartition.GetFixedWindowLimiter(
        Ip(h),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit("RateLimit:LoginPerMinute", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors("ReactFrontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<HsClassifier.Infrastructure.Seeding.BtkiCsvSeeder>();
    // Auto-seed BTKI data in development. Never implicitly against MySQL (a shared UAT database): set Seed:AutoSeed=true to force it.
    var autoSeed = builder.Configuration.GetValue<bool?>("Seed:AutoSeed")
        ?? (app.Environment.IsDevelopment()
            && !string.Equals(builder.Configuration["Database:Provider"], "MySql", StringComparison.OrdinalIgnoreCase));
    if (autoSeed)
    {
        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Hscode.csv");
        if (File.Exists(csvPath))
        {
            await seeder.SeedAsync(csvPath);
            await seeder.SeedHierarchyAsync(csvPath);
            await seeder.SeedMfnRatesAsync(Path.Combine(Path.GetDirectoryName(csvPath)!, "data", "btki_2022_full.csv"));
        }
    }
    await seeder.EnsureAdminAsync(builder.Configuration["Seed:AdminEmail"], builder.Configuration["Seed:AdminPassword"]);
}

app.Run();



