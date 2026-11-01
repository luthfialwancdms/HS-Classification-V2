using HsClassifier.Domain.Entities;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;

namespace HsClassifier.Tests;

/// <summary>Regression case 2: a code that is not exactly in the active BTKI is refused (Req 17.1).</summary>
public class ClassificationValidatorTests
{
    [Fact]
    public async Task Existing_active_code_is_valid_and_returns_the_official_description()
    {
        var m = await MiniBtki.CreateAsync();
        var (ok, failure, description) = await m.Validator.ValidateAsync("8471.30.20");

        Assert.True(ok);
        Assert.Null(failure);
        Assert.Equal("- - Laptop termasuk notebook dan subnotebook", description);
    }

    [Theory]
    [InlineData(null, "CODE_NULL")]
    [InlineData("  ", "CODE_NULL")]
    [InlineData("8471", "INVALID_FORMAT")]            // four digits are never a final recommendation
    [InlineData("8471.30", "INVALID_FORMAT")]
    [InlineData("847130200", "INVALID_FORMAT")]
    [InlineData("8471.30.2", "INVALID_FORMAT")]
    [InlineData("8471.30.99", "CODE_NOT_FOUND")]      // well formed but not in the book
    public async Task Bad_codes_are_refused_with_a_reason(string? code, string reason)
    {
        var m = await MiniBtki.CreateAsync();
        var (ok, failure, description) = await m.Validator.ValidateAsync(code);

        Assert.False(ok);
        Assert.StartsWith(reason, failure);
        Assert.Null(description);
    }

    [Fact]
    public async Task Inactive_code_is_refused()
    {
        var m = await MiniBtki.CreateAsync(inactive8518: true);
        var (ok, failure, _) = await m.Validator.ValidateAsync("8518.10.00");

        Assert.False(ok);
        Assert.StartsWith("CODE_NOT_FOUND", failure);
    }

    [Fact]
    public async Task Line_whose_heading_row_is_missing_is_refused()
    {
        var m = await MiniBtki.CreateAsync();
        var version = m.Db.NomenclatureVersions.Single();
        m.Db.BtkiTariffLines.Add(new BtkiTariffLine
        {
            VersionId = version.Id, Code = "9020.10.00", HeadingCode = "9021",   // heading 9020 never loaded
            DescriptionId = "Alat uji", IsActive = true,
        });
        await m.Db.SaveChangesAsync();

        var (ok, failure, _) = await m.Validator.ValidateAsync("9020.10.00");

        Assert.False(ok);
        Assert.StartsWith("PARENT_MISSING", failure);
    }

    [Fact]
    public async Task Without_an_active_version_nothing_is_valid()
    {
        var validator = new ClassificationValidator(new HsClassifierDbContext(AiTest.NewOptions()),
            new VersionManager(new HsClassifierDbContext(AiTest.NewOptions())));
        var (ok, failure, _) = await validator.ValidateAsync("8471.30.20");

        Assert.False(ok);
        Assert.StartsWith("NO_ACTIVE_VERSION", failure);
    }
}
