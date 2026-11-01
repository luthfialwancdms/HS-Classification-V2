using HsClassifier.Domain.Classification;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HsClassifier.Tests;

/// <summary>Usage logging must record each AI request and never change what classification sees.</summary>
public class AiUsageLoggingTests
{
    private static readonly FactSet Facts = new();

    [Fact]
    public async Task Success_is_logged_with_tokens_and_the_result_is_returned_untouched()
    {
        var inner = new FakeProvider();
        var expected = FakeProvider.Ok();
        inner.OnSuggest = () => Task.FromResult(expected);
        var rec = new CapturingRecorder();

        var result = await AiTest.Decorator(inner, rec).SuggestAsync(Facts);

        Assert.Same(expected, result);
        var e = Assert.Single(rec.Entries);
        Assert.Equal((AiCallKind.TermSuggest, AiCallStatus.Success, null), (e.Kind, e.Status, e.ErrorCode));
        Assert.Equal(("openai-compatible", "api.groq.com", "test-model", 200), (e.Provider, e.Host, e.Model, e.HttpStatus));
        Assert.Equal((40, 12, 52), (e.PromptTokens, e.CompletionTokens, e.TotalTokens));
        Assert.InRange(e.LatencyMs, 0, 5000);
    }

    [Fact]
    public async Task Http_failure_is_logged_as_failed_with_its_code_and_the_result_is_returned_untouched()
    {
        var inner = new FakeProvider();
        var failed = new SuggestionResult(null, true, new("openai-compatible", "api.groq.com", 503, "HTTP_503", null, null, null));
        inner.OnSuggest = () => Task.FromResult(failed);
        var rec = new CapturingRecorder();

        var result = await AiTest.Decorator(inner, rec).SuggestAsync(Facts);

        Assert.Same(failed, result);
        var e = Assert.Single(rec.Entries);
        Assert.Equal((AiCallStatus.Failed, "HTTP_503", 503), (e.Status, e.ErrorCode, e.HttpStatus));
        Assert.Null(e.TotalTokens);
    }

    [Fact]
    public async Task Missing_key_makes_no_request_so_nothing_is_logged()
    {
        var inner = new FakeProvider
        {
            OnSuggest = () => Task.FromResult(new SuggestionResult(null, true, new("openai-compatible", null, null, AiCall.NoKey, null, null, null))),
        };
        var rec = new CapturingRecorder();

        var result = await AiTest.Decorator(inner, rec).SuggestAsync(Facts);

        Assert.True(result.Unavailable);
        Assert.Empty(rec.Entries);
    }

    [Fact]
    public async Task A_provider_without_call_metadata_is_still_logged_from_its_outcome()
    {
        var inner = new FakeProvider { OnSuggest = () => Task.FromResult(new SuggestionResult(null, true)) };
        var rec = new CapturingRecorder();

        await AiTest.Decorator(inner, rec).SuggestAsync(Facts);

        var e = Assert.Single(rec.Entries);
        Assert.Equal((AiCallStatus.Failed, AiCall.Error, "openai-compatible"), (e.Status, e.ErrorCode, e.Provider));
    }

    [Fact]
    public async Task Provider_exception_is_logged_and_rethrown_unchanged()
    {
        var boom = new InvalidCastException("provider bug");
        var inner = new FakeProvider { OnSuggest = () => throw boom };
        var rec = new CapturingRecorder();

        var thrown = await Assert.ThrowsAsync<InvalidCastException>(() => AiTest.Decorator(inner, rec).SuggestAsync(Facts));

        Assert.Same(boom, thrown);
        Assert.Equal(AiCall.Error, Assert.Single(rec.Entries).ErrorCode);
    }

    [Fact]
    public async Task Cancellation_is_logged_and_still_propagates()
    {
        var inner = new FakeProvider { OnSuggest = () => throw new OperationCanceledException() };
        var rec = new CapturingRecorder();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AiTest.Decorator(inner, rec).SuggestAsync(Facts));

        Assert.Equal(AiCall.Cancelled, Assert.Single(rec.Entries).ErrorCode);
    }

    [Fact]
    public async Task A_recorder_that_throws_never_breaks_the_classification_call()
    {
        var inner = new FakeProvider();
        var expected = FakeProvider.Ok();
        inner.OnSuggest = () => Task.FromResult(expected);
        var rec = new CapturingRecorder { Throw = true };

        var result = await AiTest.Decorator(inner, rec).SuggestAsync(Facts); // must not throw

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task A_database_write_failure_is_swallowed_by_the_real_recorder()
    {
        using var sp = AiTest.Services("write-fails", failingWrites: true);
        var recorder = new AiUsageRecorder(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<AiUsageRecorder>.Instance);
        var inner = new FakeProvider();
        var expected = FakeProvider.Ok();
        inner.OnSuggest = () => Task.FromResult(expected);

        var result = await AiTest.Decorator(inner, recorder).SuggestAsync(Facts); // must not throw

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task The_real_recorder_writes_through_its_own_context_so_the_callers_pending_changes_stay_unsaved()
    {
        var name = Guid.NewGuid().ToString();
        using var sp = AiTest.Services(name);
        var recorder = new AiUsageRecorder(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<AiUsageRecorder>.Instance);

        // The caller (the classifier) has an unsaved change in ITS context.
        await using var callers = new HsClassifierDbContext(AiTest.NewOptions(name));
        callers.ClassificationCases.Add(new ClassificationCase { UserId = 1 });

        var inner = new FakeProvider();
        await AiTest.Decorator(inner, recorder).SuggestAsync(Facts);

        await using var check = new HsClassifierDbContext(AiTest.NewOptions(name));
        Assert.Single(check.AiUsageLogs);
        Assert.Empty(check.ClassificationCases); // still pending in the caller's context
    }

    [Fact]
    public void Model_and_provider_pass_through()
    {
        var d = AiTest.Decorator(new FakeProvider { Model = "m-1" }, new CapturingRecorder());
        Assert.Equal("m-1", d.Model);
    }

    [Fact]
    public void The_log_row_has_no_column_that_could_hold_a_prompt_key_response_or_goods_term()
    {
        var columns = typeof(AiUsageLog).GetProperties().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[]
        {
            "CaseId", "CompletionTokens", "CreatedAt", "ErrorCode", "Host", "HttpStatus", "Id", "Kind",
            "LatencyMs", "Model", "PromptTokens", "Provider", "Status", "TotalTokens",
        }, columns);
    }
}
