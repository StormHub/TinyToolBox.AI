using Azure;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Maps.Routing;
using Azure.Maps.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyToolBox.AI.Agents.Maps;
using TinyToolBox.AI.Agents.Routing;

namespace TinyToolBox.AI.Agents;

public static class DependencyInjection
{
    public static IServiceCollection AddMaps(this IServiceCollection services, IConfiguration configuration)
    {
        var mapOptions = configuration
            .GetSection(nameof(AzureMapOptions))
            .Get<AzureMapOptions>()
            ?? throw new InvalidOperationException("Azure map configuration required");
        if (string.IsNullOrEmpty(mapOptions.ApiKey) 
            && string.IsNullOrEmpty(mapOptions.ClientId))
        {
            throw new InvalidOperationException(
                $"{nameof(MapsSearchClient)} requires either api key or client id credential.");
        }
        
        services.AddOptions<AzureMapOptions>()
            .BindConfiguration(nameof(AzureMapOptions))
            .ValidateDataAnnotations();

        services.AddHttpClient(nameof(MapsSearchClient));
        services.AddTransient(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(nameof(MapsSearchClient));

            if (!string.IsNullOrEmpty(mapOptions.ApiKey))
                return new MapsSearchClient(
                    new AzureKeyCredential(mapOptions.ApiKey),
                    new MapsSearchClientOptions
                    {
                        Transport = new HttpClientTransport(httpClient)
                    });

            return new MapsSearchClient(
                new DefaultAzureCredential(),
                mapOptions.ClientId,
                new MapsSearchClientOptions
                {
                    Transport = new HttpClientTransport(httpClient)
                });
        });
        services.AddTransient<MapPlugin>();

        services.AddHttpClient(nameof(MapsRoutingClient));
        services.AddTransient(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(nameof(MapsRoutingClient));

            if (!string.IsNullOrEmpty(mapOptions.ApiKey))
                return new MapsRoutingClient(
                    new AzureKeyCredential(mapOptions.ApiKey),
                    new MapsRoutingClientOptions
                    {
                        Transport = new HttpClientTransport(httpClient)
                    });

            return new MapsRoutingClient(
                new DefaultAzureCredential(),
                mapOptions.ClientId,
                new MapsRoutingClientOptions
                {
                    Transport = new HttpClientTransport(httpClient)
                });
        });
        services.AddTransient<RoutingPlugin>();

        return services;
    }
}