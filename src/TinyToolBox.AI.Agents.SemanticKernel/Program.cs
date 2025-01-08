using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.SemanticKernel;
using TinyToolBox.AI.Agents;
using TinyToolBox.AI.Agents.Search;
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
            services.AddSearch(builderContext.Configuration);

            var openAIConfig = builderContext.Configuration
                .GetSection(nameof(AzureOpenAIConfig))
                .Get<AzureOpenAIConfig>()
                ?? throw new InvalidOperationException("Azure OpenAI configuration required");

            services.AddHttpClient(nameof(AzureOpenAIConfig))
                .AddStandardResilienceHandler()
                .Configure(options =>
                {
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                    options.Retry.MaxRetryAttempts = 10;
                });
            
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

                builder.UseSearch(provider);

                return builder;
            });

        }).Build();

    await host.StartAsync();

    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    await using (var scope = host.Services.CreateAsyncScope())
    {
        var kernelBuilder = scope.ServiceProvider.GetRequiredService<IKernelBuilder>();
        var process = ResearchProcess.Build();
        
        var kernel = kernelBuilder.Build();
        await process.StartAsync(kernel, new KernelProcessEvent { Id = "UserStep" });
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