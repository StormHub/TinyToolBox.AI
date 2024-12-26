
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using TinyToolBox.AI.Agents.SemanticKernel;
using TinyToolBox.AI.ChatCompletion.SemanticKernel;

IHost? host = default;

try
{
    host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((builderContext, builder) =>
        {
            builder.AddJsonFile("appsettings.json", false);
            builder.AddJsonFile($"appsettings.{builderContext.HostingEnvironment.EnvironmentName}.json", true);

            if (builderContext.HostingEnvironment.IsDevelopment()) builder.AddUserSecrets<Program>();

            builder.AddEnvironmentVariables();
        })
        .ConfigureServices((builderContext, services) =>
        {
            var cosmosDbConfig = builderContext.Configuration
                .GetSection(nameof(AzureCosmosDbConfig))
                .Get<AzureCosmosDbConfig>()
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

            var openAIConfig = builderContext.Configuration
                .GetSection(nameof(AzureOpenAIConfig))
                .Get<AzureOpenAIConfig>()
                ?? throw new InvalidOperationException("Azure OpenAI configuration required");

            services.AddHttpClient(openAIConfig.ChatCompletionDeployment);

            services.AddTransient(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient(openAIConfig.ChatCompletionDeployment);
                
                var builder = Kernel.CreateBuilder();
                if (!string.IsNullOrEmpty(openAIConfig.APIKey))
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: openAIConfig.ChatCompletionDeployment,
                        endpoint: openAIConfig.Endpoint,
                        openAIConfig.APIKey,
                        httpClient: httpClient);
                }
                else
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: openAIConfig.ChatCompletionDeployment,
                        endpoint: openAIConfig.Endpoint,
                        new DefaultAzureCredential(),
                        httpClient: httpClient);
                }

                return builder.Build();
            });

            services.AddTransient<TestAgent>();
        }).Build();

    await host.StartAsync();

    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    await using (var scope = host.Services.CreateAsyncScope())
    {
        var testAgent = scope.ServiceProvider.GetRequiredService<TestAgent>();
        await testAgent.Run(lifetime.ApplicationStopping);
    }

    lifetime.StopApplication();
    await host.WaitForShutdownAsync(lifetime.ApplicationStopping);
}
catch (Exception ex)
{
    Console.WriteLine($"Host terminated unexpectedly! \n{ex}");
}
finally
{
    host?.Dispose();
}