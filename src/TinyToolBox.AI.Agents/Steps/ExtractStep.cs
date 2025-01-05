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
        var results = new Dictionary<Uri, string>();
        
        var web = new HtmlWeb();
        foreach (var searchResult in input)
        {
            Console.WriteLine($"Researcher > Extracting '{searchResult.Uri}'");
            var document =
                await web.LoadFromWebAsync(searchResult.Uri.ToString(), cancellationToken: cancellationToken);
            logger.LogInformation("{Url} downloaded", searchResult.Uri);

            Console.WriteLine($"Researcher > Summarizing '{searchResult.Uri}'");
            var summary = await Summarize(document.DocumentNode.InnerText, kernel, cancellationToken);
            results.Add(searchResult.Uri, summary);
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