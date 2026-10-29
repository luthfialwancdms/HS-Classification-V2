using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Tests;

/// <summary>History visibility: users see their own cases; Admin/ExpertReviewer see all.</summary>
public class CaseHistoryTests
{
    private static async Task<HsClassifierDbContext> Seed()
    {
        var db = new HsClassifierDbContext(new DbContextOptionsBuilder<HsClassifierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.ClassificationCases.AddRange(
            new ClassificationCase { UserId = 1, Status = CaseStatus.Determined, FinalCode = "8471.30.20",
                FactsJson = """{"commercial_name":"Laptop"}""", CreatedAt = new DateTime(2026, 10, 1) },
            new ClassificationCase { UserId = 1, Status = CaseStatus.NeedExpertReview,
                FactsJson = """{"commercial_name":"Blorptron"}""", CreatedAt = new DateTime(2026, 10, 5) },
            new ClassificationCase { UserId = 2, Status = CaseStatus.Determined, FinalCode = "8517.13.00",
                FactsJson = """{"commercial_name":"Handphone"}""", CreatedAt = new DateTime(2026, 10, 7) });
        await db.SaveChangesAsync();
        return db;
    }

    [Theory]
    [InlineData("Classifier", false)]
    [InlineData("Viewer", false)]
    [InlineData("ExpertReviewer", true)]
    [InlineData("Admin", true)]
    public void Role_visibility(string role, bool seesAll) => Assert.Equal(seesAll, CaseQueries.SeesAll([role]));

    [Fact]
    public async Task Classifier_sees_only_own_cases_reviewer_sees_all()
    {
        var db = await Seed();
        Assert.Equal(2, await db.ClassificationCases.Visible(userId: 1, seesAll: false).CountAsync());
        Assert.Equal(1, await db.ClassificationCases.Visible(userId: 2, seesAll: false).CountAsync());
        Assert.Equal(3, await db.ClassificationCases.Visible(userId: 2, seesAll: true).CountAsync());
    }

    [Fact]
    public async Task Filters_by_status_text_and_inclusive_date_range()
    {
        var db = await Seed();
        var all = db.ClassificationCases.Visible(1, seesAll: true);
        Assert.Equal(1, await all.Filter(CaseStatus.NeedExpertReview, null, null, null).CountAsync());
        Assert.Equal(1, await all.Filter(null, "Handphone", null, null).CountAsync());      // facts text
        Assert.Equal(1, await all.Filter(null, "8471", null, null).CountAsync());           // final code
        Assert.Equal(2, await all.Filter(null, null, new DateTime(2026, 10, 5), new DateTime(2026, 10, 7)).CountAsync());
    }
}
