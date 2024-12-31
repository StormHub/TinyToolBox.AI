using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TinyToolBox.AI.Agents.SemanticKernel.Cosmos;

internal static class DependencyInjection
{
    public static IServiceCollection UseCosmosHistory(this IServiceCollection services, IConfiguration configuration)
    {
        var cosmosDbConfig = configuration
            .GetSection(nameof(AzureCosmosOptions))
            .Get<AzureCosmosOptions>()
            ?? throw new InvalidOperationException("Azure CosmosDB configuration required");

        // CosmosClient configuration
        services.AddHttpClient(nameof(CosmosClient));
        services.AddTransient<CosmosClient>(provider =>
        {
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                
            var options = new CosmosClientOptions
            {
                HttpClientFactory = () =>
                {
                    var factory = provider.GetRequiredService<IHttpClientFactory>();
                    return factory.CreateClient(nameof(CosmosClient));
                },
                UseSystemTextJsonSerializerWithOptions = jsonSerializerOptions
            };
                
            var endpoint = cosmosDbConfig.Endpoint;
            var apiKey = cosmosDbConfig.APIKey;
                
            return !string.IsNullOrEmpty(apiKey) 
                ? new CosmosClient(endpoint, new AzureKeyCredential(apiKey), options) 
                : new CosmosClient(endpoint, new DefaultAzureCredential(), options);
        });
        
        return services;
    }
}