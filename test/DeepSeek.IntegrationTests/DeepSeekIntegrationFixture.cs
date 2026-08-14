using DeepSeek.Core;
using Microsoft.Extensions.Configuration;

namespace DeepSeek.IntegrationTests;

public sealed class DeepSeekIntegrationFixture : IDisposable
{
    public DeepSeekIntegrationFixture()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddUserSecrets<DeepSeekIntegrationFixture>()
            .Build();

        ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
            ?? configuration["apiKey"]
            ?? throw new InvalidOperationException(
                "apiKey is not configured. Set the DEEPSEEK_API_KEY environment variable or use `dotnet user-secrets set \"apiKey\" \"<your-key>\" --project test/DeepSeek.IntegrationTests/DeepSeek.IntegrationTests.csproj`."
            );

        Client = new DeepSeekClient(ApiKey);
        Client.SetTimeout(120);
    }

    public string ApiKey { get; }

    public DeepSeekClient Client { get; }

    public CancellationToken CreateToken(int seconds = 120)
    {
        var source = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        return source.Token;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
