
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TinyToolBox.AI.Evaluation.Extensions;
using TinyToolBox.AI.Evaluation.SemanticKernel.Properties;

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

            services.AddHttpClient(nameof(Kernel));

            services.AddTransient(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient(nameof(Kernel));
                
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
            
            
        })
        .Build();

    await host.StartAsync();

    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    await using (var scope = host.Services.CreateAsyncScope())
    {
        var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();
        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        
        var templateConfigurations = Templates.DefaultConfigurations().ToArray();
        var templateConfig = templateConfigurations.First(x => string.Equals(x.Name, "humor"));

        var factory = new KernelPromptTemplateFactory();
        var template = factory.Create(templateConfig);
        
        var arguments = new KernelArguments
        {
            ["output"] = "Brown fox"
        };

        var content = await template.RenderAsync(kernel, arguments, cancellationToken: lifetime.ApplicationStopping);
        var chatHistory = new ChatHistory(content);
        var answer = await chatCompletion.GetChatMessageContentAsync(chatHistory);

        Console.WriteLine($"{answer}");
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