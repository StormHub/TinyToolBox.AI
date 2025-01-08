using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
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

        Console.WriteLine($"Researcher > Input search results : '{input.Count}'");
        // var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        using (var playwright = await Playwright.CreateAsync())
        {
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            foreach (var searchResult in input)
            {
                var text = await GetContent(browser, searchResult.Uri, logger);
                text = text?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    Console.WriteLine($"{searchResult.Uri}\n{text}");
                    results.Add(searchResult.Uri, text);
                }
            }
            await browser.CloseAsync();
        }

        var keys = results.Keys.ToArray();
        foreach (var uri in keys)
        {
            Console.WriteLine($"Researcher > Summarizing '{uri}'");
            var summary = await Summarize(results[uri], kernel, cancellationToken);
            results[uri] = summary;
        }
        
        await context.EmitEventAsync(
            new KernelProcessEvent
            {
                Id = Done,
                Data = results.AsReadOnly()
            });
    }

    private static async Task<string?> GetContent(IBrowser browser, Uri uri, ILogger logger)
    {
        IPage? page = default;
        try
        {
            page = await browser.NewPageAsync();
            await page.GotoAsync(uri.ToString());
            var handle = await page.QuerySelectorAsync("article")
                         ?? await page.QuerySelectorAsync("main");
            if (handle is not null)
            {
                return await handle.TextContentAsync();
            }

            // "content", "main-content", "post-content"
            var elements = await page.QuerySelectorAllAsync("content");
            if (elements.Count == 0)
            {
                elements = await page.QuerySelectorAllAsync("main-content");
            }
            if (elements.Count == 0)
            {
                elements = await page.QuerySelectorAllAsync("post-content");
            }

            var contents = elements.ToArray();
            if (contents.Length > 0)
            {
                var buffer = new StringBuilder();
                foreach (var content in contents)
                {
                    var text = await content.TextContentAsync();
                    text = text?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        buffer.AppendLine(text);
                    }
                }
                return buffer.ToString();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to get {Url}", uri);
        }
        finally
        {
            if (page is not null)
            {
                await page.CloseAsync();
            }
        }

        return default;
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