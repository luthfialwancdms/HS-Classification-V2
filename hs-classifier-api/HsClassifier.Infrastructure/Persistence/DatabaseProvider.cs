using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HsClassifier.Infrastructure.Persistence;

/// <summary>
/// Picks the database engine from configuration. <c>Database:Provider</c> is <c>SqlServer</c> (default, uses
/// <c>ConnectionStrings:DefaultConnection</c>) or <c>MySql</c> (uses <c>ConnectionStrings:MySql</c>). For MySQL,
/// <c>Database:MySqlVersion</c> (for example 8.0.36) avoids a round trip to the server just to learn its version.
/// </summary>
public static class DatabaseProvider
{
    // ponytail: auto-detect opens a connection, so the answer is cached per connection string. Set Database:MySqlVersion to skip it.
    private static readonly ConcurrentDictionary<string, ServerVersion> Detected = new();

    public static DbContextOptionsBuilder UseHsClassifierDatabase(this DbContextOptionsBuilder options, IConfiguration config)
    {
        var provider = (config["Database:Provider"] ?? "SqlServer").Trim();
        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            return options.UseSqlServer(config.GetConnectionString("DefaultConnection"));

        if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
        {
            var cs = config.GetConnectionString("MySql");
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("Database:Provider is MySql but ConnectionStrings:MySql is missing. Set it with dotnet user-secrets.");
            var configured = config["Database:MySqlVersion"];
            var version = Version.TryParse(configured, out var v)
                ? new MySqlServerVersion(v)
                : Detected.GetOrAdd(cs, ServerVersion.AutoDetect);
            return options.UseMySql(cs, version);
        }

        throw new InvalidOperationException($"Database:Provider '{provider}' is not supported. Use SqlServer or MySql.");
    }

    public static DbContextOptionsBuilder<TContext> UseHsClassifierDatabase<TContext>(
        this DbContextOptionsBuilder<TContext> options, IConfiguration config) where TContext : DbContext
    {
        UseHsClassifierDatabase((DbContextOptionsBuilder)options, config);
        return options;
    }
}
