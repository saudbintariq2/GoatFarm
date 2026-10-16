using Microsoft.Extensions.Configuration;

namespace GoatFarm.Infrastructure.Configuration;

public sealed class OpenAiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4.1-mini";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxOutputTokens { get; set; } = 2048;
    public string? BaseUrl { get; set; }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    public static OpenAiOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("OpenAI");
        var timeoutText = section["TimeoutSeconds"];
        var tokensText = section["MaxOutputTokens"];
        var timeout = 60;
        var tokens = 2048;
        if (int.TryParse(timeoutText, out var parsedTimeout))
            timeout = Math.Clamp(parsedTimeout, 5, 120);
        if (int.TryParse(tokensText, out var parsedTokens))
            tokens = Math.Clamp(parsedTokens, 256, 8192);

        var model = section["Model"]?.Trim();
        return new OpenAiOptions
        {
            ApiKey = section["ApiKey"]?.Trim() ?? "",
            Model = string.IsNullOrWhiteSpace(model) ? "gpt-4.1-mini" : model,
            TimeoutSeconds = timeout,
            MaxOutputTokens = tokens,
            BaseUrl = string.IsNullOrWhiteSpace(section["BaseUrl"]) ? null : section["BaseUrl"]!.Trim()
        };
    }
}
