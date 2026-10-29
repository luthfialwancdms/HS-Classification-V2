using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Tests;

/// <summary>Experts decide; the BTKI database still authorises the code (Req 17.1).</summary>
public class ExpertReviewTests
{
    private static async Task<(HsClassifierDbContext Db, ExpertReviewService Svc, long CaseId)> Setup(
        CaseStatus status = CaseStatus.NeedExpertReview, string? finalCode = null, string candidates = """["8517"]""")
    {
        var db = new HsClassifierDbContext(new DbContextOptionsBuilder<HsClassifierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var v = new NomenclatureVersion { Name = "BTKI 2022", EffectiveFrom = new DateTime(2022, 4, 1),
            State = ImportState.Active, IsActive = true };
        db.NomenclatureVersions.Add(v);
        foreach (var (code, heading, desc) in new[]
                 {
                     ("8517", "8517", "Perangkat telepon"), ("8517.13.00", "8517", "Smartphone"),
                     ("8471", "8471", "Mesin pengolah data"), ("8471.30.20", "8471", "Laptop"),
                 })
            db.BtkiTariffLines.Add(new BtkiTariffLine { VersionId = v.Id, Code = code, HeadingCode = heading,
                DescriptionId = desc, IsActive = true });
        var c = new ClassificationCase { UserId = 7, VersionId = v.Id, Status = status, FinalCode = finalCode,
            CandidateCodesJson = candidates };
        db.ClassificationCases.Add(c);
        await db.SaveChangesAsync();
        return (db, new ExpertReviewService(db, new ClassificationValidator(db, new VersionManager(db))), c.Id);
    }

    [Fact]
    public async Task Approve_within_candidate_heading_is_EXPERT_APPROVED_and_audited()
    {
        var (db, svc, id) = await Setup();
        var r = await svc.ApproveAsync(id, "8517.13.00", "Smartphone sesuai spesifikasi", reviewerId: 3);

        Assert.True(r.Ok, r.Error);
        Assert.Equal(ExpertReviewService.Approved, r.Action);
        var c = await db.ClassificationCases.FindAsync(id);
        Assert.Equal(CaseStatus.Determined, c!.Status);
        Assert.Equal("8517.13.00", c.FinalCode);
        Assert.Equal("Smartphone", c.FinalDescription); // official text from DB, not from the reviewer
        var corr = Assert.Single(db.ExpertCorrections);
        Assert.Equal(("NeedExpertReview", "Determined", 3), (corr.BeforeStatus, corr.AfterStatus, corr.ReviewerId));
        Assert.Single(db.ReasoningEvents, e => e.RuleApplied == "EXPERT_REVIEW");
    }

    [Fact]
    public async Task Valid_code_outside_candidates_is_EXPERT_OVERRIDE()
    {
        var (_, svc, id) = await Setup();
        var r = await svc.ApproveAsync(id, "8471.30.20", "Barang sebenarnya laptop", 3);
        Assert.Equal(ExpertReviewService.Override, r.Action);
    }

    [Theory]
    [InlineData("9999.99.99", "Alasan yang cukup")] // not in BTKI → validator rejects
    [InlineData("8517",       "Alasan yang cukup")] // heading, not a tariff line → invalid format
    [InlineData("8517.13.00", "ok")]                // reason too short
    [InlineData("",           "Alasan yang cukup")]
    public async Task Invalid_approvals_are_refused_and_nothing_changes(string code, string reason)
    {
        var (db, svc, id) = await Setup();
        var r = await svc.ApproveAsync(id, code, reason, 3);
        Assert.False(r.Ok);
        Assert.Equal(CaseStatus.NeedExpertReview, (await db.ClassificationCases.FindAsync(id))!.Status);
        Assert.Empty(db.ExpertCorrections);
    }

    [Fact]
    public async Task Only_cases_in_review_can_be_decided()
    {
        var (_, svc, id) = await Setup(CaseStatus.Determined, "8517.13.00");
        Assert.False((await svc.ApproveAsync(id, "8517.13.00", "Alasan yang cukup", 3)).Ok);
        Assert.False((await svc.RejectAsync(id, "Alasan yang cukup", 3)).Ok);
    }

    [Fact]
    public async Task Reject_sets_rejected_and_clears_code()
    {
        var (db, svc, id) = await Setup();
        Assert.True((await svc.RejectAsync(id, "Data barang tidak memadai", 3)).Ok);
        var c = await db.ClassificationCases.FindAsync(id);
        Assert.Equal((CaseStatus.Rejected, (string?)null), (c!.Status, c.FinalCode));
    }

    [Fact]
    public async Task Request_review_moves_determined_case_to_queue_keeping_code_as_candidate()
    {
        var (db, svc, id) = await Setup(CaseStatus.Determined, "8517.13.00", candidates: "[]");
        Assert.True((await svc.RequestReviewAsync(id, "Ragu dengan subpos", 7)).Ok);
        var c = await db.ClassificationCases.FindAsync(id);
        Assert.Equal(CaseStatus.NeedExpertReview, c!.Status);
        Assert.Null(c.FinalCode);
        Assert.Contains("8517.13.00", c.CandidateCodesJson);

        // and the expert can then confirm the original code as a candidate approval
        Assert.Equal(ExpertReviewService.Approved, (await svc.ApproveAsync(id, "8517.13.00", "Sudah benar", 3)).Action);
    }
}
