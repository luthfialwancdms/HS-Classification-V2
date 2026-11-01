using System.Text.Json;
using HsClassifier.Infrastructure.Services;

namespace HsClassifier.Tests;

public class AiUsageParsingTests
{
    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement.Clone();

    [Fact]
    public void OpenAi_usage_is_read()
    {
        var u = AiCall.OpenAiUsage(Json("""{"choices":[],"usage":{"prompt_tokens":40,"completion_tokens":12,"total_tokens":52}}"""));
        Assert.Equal((40, 12, 52), u);
    }

    [Fact]
    public void Total_is_computed_when_only_prompt_and_completion_are_given()
    {
        Assert.Equal((10, 5, 15), AiCall.OpenAiUsage(Json("""{"usage":{"prompt_tokens":10,"completion_tokens":5}}""")));
    }

    [Fact]
    public void Gemini_usage_is_read()
    {
        var u = AiCall.GeminiUsage(Json("""{"usageMetadata":{"promptTokenCount":7,"candidatesTokenCount":3,"totalTokenCount":25}}"""));
        Assert.Equal((7, 3, 25), u); // the provider's own total wins (it can include thinking tokens)
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"usage":null}""")]
    [InlineData("""{"usage":"many"}""")]
    [InlineData("""{"usage":{"prompt_tokens":"ten","completion_tokens":-1}}""")]
    [InlineData("""[]""")]
    public void Missing_or_malformed_usage_gives_nulls_not_errors(string json)
    {
        Assert.Equal((null, null, null), AiCall.OpenAiUsage(Json(json)));
    }

    [Theory]
    [InlineData("https://api.groq.com/openai/v1", "api.groq.com")]
    [InlineData("https://proxy.example.com:8443/v1/?token=secret", "proxy.example.com")] // path and query never kept
    [InlineData("not a url", null)]
    public void Host_keeps_the_host_only(string url, string? expected) => Assert.Equal(expected, AiCall.HostOf(url));

    [Fact]
    public void Exceptions_map_to_short_codes()
    {
        Assert.Equal(AiCall.Timeout, AiCall.Classify(new TaskCanceledException()));
        Assert.Equal(AiCall.Network, AiCall.Classify(new HttpRequestException("secret detail")));
        Assert.Equal(AiCall.Parse, AiCall.Classify(new JsonException()));
        Assert.Equal(AiCall.Error, AiCall.Classify(new ArgumentException()));
    }

    [Theory]
    [InlineData("HTTP_401", "kredensial")]
    [InlineData("HTTP_403", "kredensial")]
    [InlineData("HTTP_404", "model")]
    [InlineData("HTTP_429", "Batas permintaan")]
    [InlineData("HTTP_503", "bermasalah")]
    [InlineData("NO_KEY", "belum dikonfigurasi")]
    [InlineData("TIMEOUT", "10 detik")]
    [InlineData("NETWORK", "terhubung")]
    public void Error_messages_are_readable_indonesian(string code, string contains)
    {
        Assert.Contains(contains, AiErrorMessages.For(code), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Error_messages_never_echo_the_code_payload_or_credentials()
    {
        Assert.Null(AiErrorMessages.For(null));
        var msg = AiErrorMessages.For("HTTP_401 Bearer gsk_secret");
        Assert.DoesNotContain("gsk_", msg);
        Assert.DoesNotContain("Bearer", msg);
    }
}
