using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Text;

using Tokenizer = Microsoft.ML.Tokenizers.Tokenizer;

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

            var text = GetContent(document.DocumentNode) 
                       ?? searchResult.Description;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            Console.WriteLine($"Researcher > Summarizing '{searchResult.Uri}'");
            var summary = await Summarize(text, kernel, cancellationToken);
            results.Add(searchResult.Uri, summary);
        }
        
        await context.EmitEventAsync(
            new KernelProcessEvent
            {
                Id = Done,
                Data = results.AsReadOnly()
            });
    }

    private static string? GetContent(HtmlNode root)
    {
        // meta
        var metaNodes = root.QuerySelectorAll("meta");
        if (metaNodes.Count > 0)
        {
            foreach (var metaNode in metaNodes)
            {
                var attribute = metaNode.Attributes.FirstOrDefault(
                    x => string.Equals(x.Name, "content", StringComparison.OrdinalIgnoreCase));
                if (attribute is not null 
                    && attribute.Value.Contains("noindex", StringComparison.OrdinalIgnoreCase))
                {
                    return default;
                }
            }
        }
        
        // "article", "main"
        var node = root.QuerySelector("article")
                   ?? root.QuerySelector("main");
        if (node is not null)
        {
            return node.InnerText;
        }

        // "content", "main-content", "post-content"
        var nodes = root.QuerySelectorAll("content");
        if (nodes.Count == 0)
        {
            nodes = root.QuerySelectorAll("main-content");
        }
        if (nodes.Count == 0)
        {
            nodes = root.QuerySelectorAll("post-content");
        }
        return nodes.Count > 0 
            ? string.Join('\n', nodes.Select(x => x.InnerText))
            : default;
    }

    private static async Task<string> Summarize(string text, Kernel kernel, CancellationToken cancellationToken = default)
    {
        var tokenizer = kernel.Services.GetRequiredService<Tokenizer>();
        
        var lines = TextChunker.SplitPlainTextLines(
            text, 
            maxTokensPerLine: 100, 
            tokenCounter: input => tokenizer.CountTokens(input));
        var paragraphs = TextChunker.SplitPlainTextParagraphs(
            lines, 
            maxTokensPerParagraph: 255,
            tokenCounter: input => tokenizer.CountTokens(input));

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