using System;
using DeepSeek.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DeepSeek.AspNetCore;
public static class Extension
{
    public static IServiceCollection AddDeepSeek(this IServiceCollection services, Action<HttpClient> option)
    {
        services.AddHttpClient<DeepSeekClient>(option);
        services.AddScoped(provider =>
        {
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(DeepSeekClient));
            return new DeepSeekClient(httpClient);
        });
        return services;
    }
}
