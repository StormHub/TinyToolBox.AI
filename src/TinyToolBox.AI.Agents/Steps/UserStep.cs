using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace TinyToolBox.AI.Agents.Steps;

internal record UserState
{
    public string Input { get; set; } = string.Empty;
    
    public string Query { get; set; } = string.Empty;
}

internal sealed class UserStep : KernelProcessStep<UserState>
{
    public static readonly string Done = $"{nameof(UserStep)}.{nameof(Done)}";
    
    private UserState? _state;
    
    public override ValueTask ActivateAsync(KernelProcessStepState<UserState> state)
    {
        _state = state.State;
        return ValueTask.CompletedTask;
    }

    [KernelFunction(nameof(GetInput))]
    public async Task GetInput(KernelProcessStepContext context, Kernel kernel, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Researcher > What do you want to research for?");
        Console.Write("User > ");
        
        var line = Console.ReadLine();
        if (string.IsNullOrEmpty(line?.Trim()))
        {
            await context.EmitEventAsync(
                new KernelProcessEvent
                {
                    Id = "Exit"
                });
            return;
        }
        
        _state ??= new UserState();
        _state.Input = line;

        const string prompt =
            """
            Generate a search-optimized query of the input by analyzing its core semantic meaning and intent.

            input: 
            {{$input}}

            Return only the query with no additional text.
            """;

        var result = await kernel.InvokePromptAsync(prompt,
            new KernelArguments(
                new OpenAIPromptExecutionSettings
                {
                    ModelId = "gpt-4o",
                    Temperature = 0.5
                })
            {
                ["input"] = line
            }, 
            cancellationToken: cancellationToken);

        _state.Query = result.GetValue<string>() ?? string.Empty;
        
        await context.EmitEventAsync(
            new KernelProcessEvent
            {
                Id = Done,
                Data = _state.Query
            });
    }
    
    [KernelFunction(nameof(PrintResults))]
    public Task PrintResults(IReadOnlyDictionary<Uri, string> results)
    {
        foreach (var pair in results)
        {
            Console.WriteLine($"{pair.Key}\n{pair.Value}\n");
        }

        return Task.CompletedTask;
    }
    
}