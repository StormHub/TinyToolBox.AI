
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using TinyToolBox.AI.Agents;
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
            var openAIConfig = builderContext.Configuration
                .GetSection(nameof(AzureOpenAIConfig))
                .Get<AzureOpenAIConfig>()
                ?? throw new InvalidOperationException("Azure OpenAI configuration required");

            services.AddHttpClient(nameof(AzureOpenAIConfig));

            services.AddTransient(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient(nameof(AzureOpenAIConfig));
                
                var builder = Kernel.CreateBuilder();
                if (!string.IsNullOrEmpty(openAIConfig.APIKey))
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: openAIConfig.ChatCompletionDeployment,
                        endpoint: openAIConfig.Endpoint,
                        apiKey: openAIConfig.APIKey,
                        serviceId: "azure:chat",
                        httpClient: httpClient);
                }
                else
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: openAIConfig.ChatCompletionDeployment,
                        endpoint: openAIConfig.Endpoint,
                        new DefaultAzureCredential(),
                        serviceId: "azure:chat",
                        httpClient: httpClient);
                }

                return builder.Build();
            });

            services.AddMaps(builderContext.Configuration);
            
            // services.UseCosmosHistory(builderContext.Configuration);
            // services.AddTransient<CosmosChatHistoryTestAgent>();
        }).Build();

    await host.StartAsync();

    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    await using (var scope = host.Services.CreateAsyncScope())
    {
        // var testAgent = scope.ServiceProvider.GetRequiredService<CosmosChatHistoryTestAgent>();
        // await testAgent.Run(lifetime.ApplicationStopping);
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