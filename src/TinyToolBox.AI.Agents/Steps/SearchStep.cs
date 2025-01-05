using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Bing;

namespace TinyToolBox.AI.Agents.Steps;

public record SearchResult(Uri Uri, string Title, string? Description);

public record SearchStepState
{
    public List<SearchResult> Results { get; init; } = [];
}

internal sealed class SearchStep : KernelProcessStep<SearchStepState>
{
    public static readonly string Done = $"{nameof(SearchStep)}.{nameof(Done)}";

    private SearchStepState? _state;
    
    public override ValueTask ActivateAsync(KernelProcessStepState<SearchStepState> state)
    {
        _state = state.State;
        return ValueTask.CompletedTask;
    }

    [KernelFunction(nameof(Search))]
    public async Task Search(
        KernelProcessStepContext context, 
        Kernel kernel, 
        string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
        {
            await context.EmitEventAsync(
                new KernelProcessEvent
                {
                    Id = "Exit"
                });
            return;
        }

        Console.WriteLine($"Researcher > Searching '{query}'");
        var textSearch = kernel.GetRequiredService<BingTextSearch>();
        var response = await textSearch.GetSearchResultsAsync(
            query,
            new TextSearchOptions
            {
                Top = 5,
                IncludeTotalCount = true
            },
            cancellationToken);

        var searchResults = new Dictionary<Uri, SearchResult>();
        await foreach (var result in AsPages(response.Results, cancellationToken))
        {
                if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var uri) 
                    || string.IsNullOrEmpty(result.Name) 
                    || searchResults.ContainsKey(uri))
                {
                    continue;
                }

                searchResults.Add(uri, new SearchResult(uri, result.Name, result.Snippet));
        }
        _state ??= new SearchStepState();
        _state.Results.AddRange(searchResults.Values);
        
        await context.EmitEventAsync(
            new KernelProcessEvent
            {
                Id = Done,
                Data = _state.Results.AsReadOnly()
            });
    }

    private static async IAsyncEnumerable<BingWebPage> AsPages(IAsyncEnumerable<object> results, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var result in results.WithCancellation(cancellationToken))
        {
            if (result is BingWebPage bingWebPage)
            {
                yield return bingWebPage;

                if (bingWebPage.IsNavigational.GetValueOrDefault() 
                    && bingWebPage.DeepLinks is { Count: > 0 } )
                {
                    foreach (var link in bingWebPage.DeepLinks)
                    {
                        yield return link;
                    }
                }
            }
        }
    }
}