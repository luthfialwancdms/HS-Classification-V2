using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HsClassifier.Tests;

/// <summary>
/// The database engine comes from configuration: SQL Server by default (as before), MySQL when Database:Provider says so.
/// The model must work on both, so nothing in it may name a SQL Server column type.
/// </summary>
public class DatabaseProviderTests
{
    private static IConfiguration Config(params (string, string?)[] kv) =>
        new ConfigurationBuilder().AddInMemoryCollection(kv.Select(x => new KeyValuePair<string, string?>(x.Item1, x.Item2))).Build();

    private static HsClassifierDbContext Context(IConfiguration cfg)
    {
        var options = new DbContextOptionsBuilder<HsClassifierDbContext>().UseHsClassifierDatabase(cfg).Options;
        return new HsClassifierDbContext(options);
    }

    private const string SqlServerCs = "Server=localhost;Database=x;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string MySqlCs = "Server=localhost;Port=3306;Database=x;User ID=u;Password=p;SslMode=Required";

    [Fact]
    public void Without_a_provider_setting_it_is_SQL_Server_as_before()
    {
        using var db = Context(Config(("ConnectionStrings:DefaultConnection", SqlServerCs)));

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);
    }

    [Fact]
    public void MySql_is_chosen_by_configuration_and_reads_its_own_connection_string()
    {
        using var db = Context(Config(("Database:Provider", "MySql"), ("Database:MySqlVersion", "8.0.36"), ("ConnectionStrings:MySql", MySqlCs), ("ConnectionStrings:DefaultConnection", SqlServerCs)));

        Assert.Equal("Pomelo.EntityFrameworkCore.MySql", db.Database.ProviderName);
        Assert.Contains("Port=3306", db.Database.GetConnectionString());
    }

    [Fact]
    public void The_provider_name_is_not_case_sensitive()
    {
        using var db = Context(Config(("Database:Provider", "mysql"), ("Database:MySqlVersion", "8.0.36"), ("ConnectionStrings:MySql", MySqlCs)));

        Assert.Equal("Pomelo.EntityFrameworkCore.MySql", db.Database.ProviderName);
    }

    [Fact]
    public void An_unknown_provider_or_a_missing_connection_string_fails_with_a_clear_message()
    {
        var unknown = Assert.Throws<InvalidOperationException>(() => Context(Config(("Database:Provider", "Oracle"))));
        Assert.Contains("Oracle", unknown.Message);

        var missing = Assert.Throws<InvalidOperationException>(() => Context(Config(("Database:Provider", "MySql"))));
        Assert.Contains("ConnectionStrings:MySql", missing.Message);
    }

    [Fact]
    public void The_model_creates_a_MySql_schema_without_SQL_Server_types()
    {
        using var db = Context(Config(("Database:Provider", "MySql"), ("ConnectionStrings:MySql", MySqlCs), ("Database:MySqlVersion", "8.0.36")));

        var script = db.Database.GenerateCreateScript();

        Assert.DoesNotContain("nvarchar", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("datetime2", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("longtext", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE `BtkiTariffLines`", script);
        Assert.Contains("CREATE TABLE `ReasoningEvents`", script);
    }

    [Fact]
    public void The_SQL_Server_schema_is_unchanged_by_the_provider_switch()
    {
        using var db = Context(Config(("ConnectionStrings:DefaultConnection", SqlServerCs)));

        var script = db.Database.GenerateCreateScript();

        // text columns without a length are still nvarchar(max), exactly what the existing migrations created
        Assert.Contains("[FactsJson] nvarchar(max)", script);
        Assert.Contains("[Reasoning] nvarchar(max)", script);
    }
}
