## Lightweight AI application evaluation templates 

The LLM-as-a-Judge approach uses large language models (LLMs) to evaluate AI-generated text based on predefined criteria.

* Implemented by [Semantic Kernel](https://github.com/microsoft/semantic-kernel)
* Prompts are from [autoevals](https://github.com/braintrustdata/autoevals)
- Battle
- ClosedQA
- Humor
- Factuality
- Moderation
- Security
- Summarization
- SQL
- Translation
- Fine-tuned binary classifiers

## Example of quick test of AI application output
Note that the name and parameters must match individual prompts above.
```csharp
// Setup semantic kernel with ChatCompletion first
// Create PromptExecutionSettings and set 'Temperature'
const string isThisFunny = "I am a brown fox";
var json = 
    $$"""
    {
        "humor" : {
            "output" : "{{isThisFunny}}"
        },
        "factuality" : {
            "input" : "What color was Cotton?",
            "output": "white",
            "expected": "white"
        }
    }
    """;
await foreach (var result in 
          kernel.Run(json, executionSettings: executionSettings))
{
    Console.WriteLine($"[{result.Key}]: result: {result.Value?.Item1}, score: {result.Value?.Item2}");
}
```

Complete example [here](https://github.com/StormHub/TinyToolBox.AI/blob/main/src/TinyToolBox.AI.Evaluation.SemanticKernel/Program.cs#L73)