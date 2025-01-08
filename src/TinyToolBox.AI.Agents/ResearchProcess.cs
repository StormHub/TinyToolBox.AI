using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using TinyToolBox.AI.Agents.Steps;

namespace TinyToolBox.AI.Agents;

public sealed class ResearchProcess : KernelProcessStep
{
    internal static readonly string Exit = nameof(Exit);
    
    public static KernelProcess Build()
    {
        var builder = new ProcessBuilder(nameof(ResearchProcess));
        
        var userInputStep = builder.AddStepFromType<UserStep>();
        var searchStep = builder.AddStepFromType<SearchStep>();
        var extractStep = builder.AddStepFromType<ExtractStep>();
         
        var errorStep = builder.AddStepFromType<ResearchProcess>();
         
        builder.OnInputEvent(nameof(UserStep))
            .SendEventTo(new ProcessFunctionTargetBuilder(userInputStep, 
                nameof(UserStep.GetInput)));

        userInputStep.OnEvent(UserStep.Done)
            .SendEventTo(new ProcessFunctionTargetBuilder(searchStep, 
                nameof(SearchStep.Search), parameterName: "query"));
         
        searchStep.OnEvent(SearchStep.Done)
            .SendEventTo(new ProcessFunctionTargetBuilder(extractStep, 
                nameof(ExtractStep.Extract), parameterName: "input"));
         
        extractStep.OnEvent(ExtractStep.Done)
            .SendEventTo(
                new ProcessFunctionTargetBuilder(userInputStep,
                    nameof(UserStep.PrintResults), parameterName: "results"));
         
        OnErrorStop(errorStep, searchStep, extractStep);
         
        userInputStep
            .OnEvent(Exit)
            .StopProcess();

        var process = builder.Build();
        return process;
    }

    private static void OnErrorStop(ProcessStepBuilder errorStep, params ProcessStepBuilder[] stepBuilders)
    {
        foreach (var stepBuilder in stepBuilders)
        {
            stepBuilder.OnFunctionError()
                .SendEventTo(new ProcessFunctionTargetBuilder(errorStep, 
                    nameof(RenderError), "error"))
                .StopProcess();
        }
    }    
 
    [KernelFunction]
    public void RenderError(KernelProcessError error, ILogger logger)
    {
        var message = string.IsNullOrWhiteSpace(error.Message) ? "Unexpected failure" : error.Message;
        Console.WriteLine($"ERROR: {message} [{error.GetType().Name}]{Environment.NewLine}{error.StackTrace}");
        logger.LogError("Unexpected failure: {ErrorMessage} [{ErrorType}]", error.Message, error.Type);
    }
}