using System.Net;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Text;

namespace TinyToolBox.AI.Agents.Steps;

internal sealed class ExtractStep : KernelProcessStep
{
    public static readonly string Done = $"{nameof(ExtractStep)}.{nameof(Done)}";

    [KernelFunction(nameof(Extract))]
    public async Task Extract(
        KernelProcessStepContext context,
        Kernel kernel,
        IReadOnlyCollection<SearchResult> input,
        CancellationToken cancellationToken = default)
    {
        var logger = kernel.LoggerFactory.CreateLogger(typeof(ExtractStep));
        
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All
        };
        var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.Add(
            "User-Agent", 
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Add(
            "Accept", 
            "text/html");
        httpClient.DefaultRequestHeaders.Add(
            "Accept-encoding",
            "gzip, deflate, br");
        httpClient.DefaultRequestHeaders.Add(
            "Accept-language",
            "en-GB,en");
        
        var results = new Dictionary<Uri, string>();

        foreach (var searchResult in input)
        {
            Console.WriteLine($"Researcher > Extracting '{searchResult.Uri}'");

            var response = await httpClient.GetAsync(searchResult.Uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStreamAsync(cancellationToken);
                var document = new HtmlDocument();
                document.Load(content);
               
                var text = document.DocumentNode.InnerText.Trim();
                var summary = await Summarize(text, kernel, cancellationToken);
                results.Add(searchResult.Uri, summary);
            }
            else
            {
                logger.LogWarning("Unable to get {Uri} {StatusCode}", searchResult.Uri, response.StatusCode);
                results.Add(searchResult.Uri, searchResult.Description);
            }
        }
        
        await context.EmitEventAsync(
            new KernelProcessEvent
            {
                Id = Done,
                Data = results.AsReadOnly()
            });
    }

    private static async Task<string> Summarize(string text, Kernel kernel, CancellationToken cancellationToken = default)
    {
        var lines = TextChunker.SplitPlainTextLines(text, maxTokensPerLine: 100);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, maxTokensPerParagraph: 255);
        
        var tokenizer = kernel.Services.GetRequiredService<Tokenizer>();

        const string prompt =
            """
            Rewrite this text in summarized form.

            Previous summary:
            {{$summary}}

            Text to summarize next:
            {{$current}}
            """;

        var summary = string.Empty;
        foreach (var chunk in Chunks(paragraphs, tokenizer))
        {
            var result = await kernel.InvokePromptAsync(prompt,
                new KernelArguments
                {
                    [ "summary" ] = summary,
                    [ "current" ] = chunk
                }, 
                cancellationToken: cancellationToken);
            var value = result.GetValue<string>();
            if (!string.IsNullOrEmpty(value))
            {
                summary = value;
            }
        }

        return summary;
    }

    private static IEnumerable<string> Chunks(IEnumerable<string> source, 
        Tokenizer tokenizer, 
        int defaultTokenLimit = 4000 /* about half of 8191 */)
    {
        var chunks = new List<string>();
        var count = 0;
        foreach (var content in source)
        {
            var token = tokenizer.CountTokens(content);
            if ((count + token) < defaultTokenLimit)
            {
                count += token;
                chunks.Add(content);
            }
            else
            {
                if (chunks.Count > 0)
                {
                    yield return string.Join('\n', chunks);
                }

                chunks = [content];
                count = token;
            }
        }
        
        if (chunks.Count > 0)
        {
            yield return string.Join('\n', chunks);
        }
    }
}