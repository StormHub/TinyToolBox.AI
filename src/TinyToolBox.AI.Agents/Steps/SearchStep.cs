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

        var searchResults = new List<SearchResult>();
        await foreach (var result in response.Results.WithCancellation(cancellationToken))
        {
            if (result is BingWebPage webPage)
            {
                if (!Uri.TryCreate(webPage.Url, UriKind.Absolute, out var uri) 
                    || string.IsNullOrEmpty(webPage.Name))
                {
                    continue;
                }
                
                searchResults.Add(new SearchResult(uri, webPage.Name, webPage.Snippet));
            }
        }
        _state ??= new SearchStepState();
        _state.Results.AddRange(searchResults);
        
        await context.EmitEventAsync(
            new KernelProcessEvent
            {
                Id = Done,
                Data = searchResults.AsReadOnly()
            });
    }
}