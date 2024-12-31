using Azure;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Maps.Routing;
using Azure.Maps.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TinyToolBox.AI.Agents.Maps;
using TinyToolBox.AI.Agents.Routing;

namespace TinyToolBox.AI.Agents;

public static class DependencyInjection
{
    public static IServiceCollection AddMaps(this IServiceCollection services)
    {
        services.AddOptions<AzureMapOptions>()
            .BindConfiguration(nameof(AzureMapOptions))
            .ValidateDataAnnotations();

        services.AddHttpClient(nameof(MapsSearchClient));
        services.AddTransient(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(nameof(MapsSearchClient));

            var mapOptions = provider.GetRequiredService<IOptions<AzureMapOptions>>().Value;
            if (!string.IsNullOrEmpty(mapOptions.ApiKey))
                return new MapsSearchClient(
                    new AzureKeyCredential(mapOptions.ApiKey),
                    new MapsSearchClientOptions
                    {
                        Transport = new HttpClientTransport(httpClient)
                    });

            if (!string.IsNullOrEmpty(mapOptions.ClientId))
                return new MapsSearchClient(
                    new DefaultAzureCredential(),
                    mapOptions.ClientId,
                    new MapsSearchClientOptions
                    {
                        Transport = new HttpClientTransport(httpClient)
                    });

            throw new InvalidOperationException(
                $"{nameof(MapsSearchClient)} requires either api key or client id credential.");
        });
        services.AddTransient<MapPlugin>();

        services.AddHttpClient(nameof(MapsRoutingClient));
        services.AddTransient(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(nameof(MapsRoutingClient));

            var mapOptions = provider.GetRequiredService<IOptions<AzureMapOptions>>().Value;
            
            if (!string.IsNullOrEmpty(mapOptions.ApiKey))
                return new MapsRoutingClient(
                    new AzureKeyCredential(mapOptions.ApiKey),
                    new MapsRoutingClientOptions
                    {
                        Transport = new HttpClientTransport(httpClient)
                    });
            
            if (!string.IsNullOrEmpty(mapOptions.ClientId))
                return new MapsRoutingClient(
                    new DefaultAzureCredential(),
                    mapOptions.ClientId,
                    new MapsRoutingClientOptions
                    {
                        Transport = new HttpClientTransport(httpClient)
                    });

            throw new InvalidOperationException(
                $"{nameof(MapsRoutingClient)} requires either api key or client id credential.");
        });
        services.AddTransient<RoutingPlugin>();

        return services;
    }
}