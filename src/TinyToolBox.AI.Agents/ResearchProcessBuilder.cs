using Microsoft.SemanticKernel;
using TinyToolBox.AI.Agents.Steps;

namespace TinyToolBox.AI.Agents;

public sealed class ResearchProcessBuilder
{
    public static KernelProcess Build(IKernelBuilder kernelBuilder)
    {
         var builder = new ProcessBuilder("ResearchProcess");
         var userInputStep = builder.AddStepFromType<UserStep>();
         var searchStep = builder.AddStepFromType<SearchStep>();
         var extractStep = builder.AddStepFromType<ExtractStep>();
         
         builder.OnInputEvent(nameof(UserStep))
             .SendEventTo(new ProcessFunctionTargetBuilder(userInputStep, nameof(UserStep.GetInput)));

         userInputStep.OnEvent(UserStep.Done)
             .SendEventTo(new ProcessFunctionTargetBuilder(searchStep, parameterName: "query"));
         
         searchStep.OnEvent(SearchStep.Done)
             .SendEventTo(new ProcessFunctionTargetBuilder(extractStep, parameterName: "input"));

         extractStep.OnEvent(ExtractStep.Done)
             .SendEventTo(
                 new ProcessFunctionTargetBuilder(userInputStep,
                     nameof(UserStep.PrintResults), parameterName: "results"));
         
         userInputStep
             .OnEvent("Exit")
             .StopProcess();

         return builder.Build();
    }
}