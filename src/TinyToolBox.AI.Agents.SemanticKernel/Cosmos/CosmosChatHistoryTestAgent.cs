using System.ComponentModel;
using Microsoft.Azure.Cosmos;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using TinyToolBox.AI.ChatCompletion.Cosmos;

namespace TinyToolBox.AI.Agents.SemanticKernel.Cosmos;

internal sealed class CosmosChatHistoryTestAgent
{
    private const string Name = "Host";
    private const string Instructions = "Answer questions about the menu.";

    private readonly Kernel _kernel;
    private readonly CosmosClient _client;
    private readonly CosmosChatHistoryStoreOptions _options;

    public CosmosChatHistoryTestAgent(Kernel kernel, CosmosClient client)
    {
        _kernel = kernel;
        _client = client;
        _options = new CosmosChatHistoryStoreOptions
        {
            DatabaseName = "agent",
            ContainerName = "history"
        };
    }

    public async Task Run(CancellationToken cancellationToken = default)
    {
        var historyStore = new CosmosChatHistoryStore(_client, _options, _kernel.LoggerFactory);
        await historyStore.CreateIfNotExists(cancellationToken);
        
        await foreach(var (id, threadId) in historyStore.GetKeys(cancellationToken))
        {
            Console.WriteLine($"History Id = {id}, threadId = {threadId}");
        }
        
        var agent = new ChatCompletionAgent
        {
            Instructions = Instructions,
            Name = Name,
            Kernel = _kernel, 
            Arguments = new KernelArguments(
                new AzureOpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                })
        };
        var plugin = KernelPluginFactory.CreateFromType<MenuPlugin>();
        agent.Kernel.Plugins.Add(plugin);

        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var today = day.ToString("yyyy-MM-dd");
        
        var chatHistory = new ChatHistory();
        await foreach (var message in historyStore.Get(today, "menu", cancellationToken))
        {
            chatHistory.Add(message);
        }
        
        await InvokeAgent(agent, chatHistory, "Hello", cancellationToken);
        await InvokeAgent(agent, chatHistory,"What is the special soup?", cancellationToken);
        await InvokeAgent(agent, chatHistory,"What is the special drink?", cancellationToken);
        await InvokeAgent(agent, chatHistory,"Thank you", cancellationToken);

        await historyStore.Upsert("2024-12-26", "menu", chatHistory, cancellationToken);
        
        Console.WriteLine($"{agent.GetType().FullName} completed");
    }

    private static async Task InvokeAgent(ChatCompletionAgent agent, ChatHistory chatHistory, string input, CancellationToken cancellationToken)
    {
        ChatMessageContent message = new(AuthorRole.User, input);
        chatHistory.Add(message);

        await foreach (var response in agent.InvokeAsync(chatHistory, cancellationToken: cancellationToken))
        {
            chatHistory.Add(response);
            WriteAgentChatMessage(response);
        }
    }
    
    private static void WriteAgentChatMessage(ChatMessageContent message)
    {
        Console.WriteLine($"{message.Role} {message}");
    }
    
    private sealed class MenuPlugin
    {
        [KernelFunction, Description("Provides a list of specials from the menu.")]
        public string GetSpecials() =>
            """
            Special Soup: Clam Chowder
            Special Salad: Cobb Salad
            Special Drink: Chai Tea
            """;

        [KernelFunction, Description("Provides the price of the requested menu item.")]
        public string GetItemPrice(
            [Description("The name of the menu item.")]
            string menuItem) =>
            "$9.99";
    }    
}