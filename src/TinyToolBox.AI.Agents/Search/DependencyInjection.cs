using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Web.Bing;

namespace TinyToolBox.AI.Agents.Search;

public static class DependencyInjection
{
    public static IServiceCollection AddSearch(this IServiceCollection services, IConfiguration configuration)
    {
        var bingOptions = configuration
            .GetSection(nameof(AzureBingOptions))
            .Get<AzureBingOptions>()
            ?? throw new InvalidOperationException("Azure bing search configuration required");
        
        services.AddHttpClient(nameof(BingTextSearch));
        services.AddTransient(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(nameof(BingTextSearch));

            var options = new BingTextSearchOptions
            {
                HttpClient = httpClient,
                LoggerFactory = provider.GetService<ILoggerFactory>()
            };
            return new BingTextSearch(bingOptions.APIKey, options);
        });
        
        return services;
    }

    public static IKernelBuilder UseSearch(this IKernelBuilder builder, IServiceProvider provider)
    {
        builder.Services.AddSingleton(provider.GetRequiredService<BingTextSearch>());
        
        var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
        builder.Services.AddSingleton<Tokenizer>(tokenizer);

        return builder;
    }
}