using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace TinyToolBox.AI.ChatCompletion.Cosmos;

public sealed class CosmosChatHistoryStore
{
    private readonly CosmosClient _client;
    private readonly ILogger _logger;
    private readonly CosmosChatHistoryStoreOptions _options;

    public CosmosChatHistoryStore(
        CosmosClient client,
        CosmosChatHistoryStoreOptions options,
        ILoggerFactory? loggerFactory = null)
    {
        _client = client;
        _options = options;
        _logger = loggerFactory?.CreateLogger(typeof(CosmosChatHistoryStore)) ?? NullLogger.Instance;
    }

    public async Task CreateIfNotExists(CancellationToken cancellationToken = default)
    {
        var databaseResponse = await _client
            .CreateDatabaseIfNotExistsAsync(_options.DatabaseName, cancellationToken: cancellationToken);

        var properties = new ContainerProperties(
            _options.ContainerName,
            CosmosChatHistory.PartitionKeyPath)
        {
            DefaultTimeToLive = _options.DefaultTimeToLiveInSeconds
        };

        var containerResponse = await databaseResponse.Database
            .CreateContainerIfNotExistsAsync(
                properties,
                cancellationToken: cancellationToken);

        _logger.LogInformation("{Database} {ContainerId} created",
            containerResponse.Container.Database,
            containerResponse.Container.Id);
    }

    public async IAsyncEnumerable<ChatMessageContent> Get(
        string sessionId,
        string threadId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var container = _client
            .GetDatabase(_options.DatabaseName)
            .GetContainer(_options.ContainerName);

        var history = await GetChatHistory(container, sessionId, threadId, cancellationToken);
        if (history is not null)
            foreach (var message in history.Messages)
                yield return ChatHistoryExtensions.ToChatMessageContent(message);
    }

    public async Task Upsert(
        string sessionId,
        string threadId,
        IEnumerable<ChatMessageContent> messages,
        CancellationToken cancellationToken = default)
    {
        var container = _client
            .GetDatabase(_options.DatabaseName)
            .GetContainer(_options.ContainerName);
        var history = await GetChatHistory(container, sessionId, threadId, cancellationToken)
                      ?? new CosmosChatHistory
                      {
                          Id = sessionId,
                          ThreadId = threadId,
                          Messages = []
                      };
        foreach (var message in messages)
        {
            var chatMessage = ChatHistoryExtensions.ToChatMessage(message);
            history.Messages.Add(chatMessage);
        }

        await container.UpsertItemAsync(
            history,
            new PartitionKey(threadId),
            cancellationToken: cancellationToken);
    }


    public async IAsyncEnumerable<ChatMessage> GetMessages(
        string sessionId,
        string threadId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var container = _client
            .GetDatabase(_options.DatabaseName)
            .GetContainer(_options.ContainerName);

        var history = await GetChatHistory(container, sessionId, threadId, cancellationToken);
        if (history is not null)
            foreach (var message in history.Messages)
                yield return message;
    }

    public async Task Delete(string sessionId, string threadId, CancellationToken cancellationToken = default)
    {
        var container = _client
            .GetDatabase(_options.DatabaseName)
            .GetContainer(_options.ContainerName);

        await container.DeleteItemAsync<CosmosChatHistory>(
            sessionId,
            new PartitionKey(threadId),
            cancellationToken: cancellationToken);
    }

    public async IAsyncEnumerable<(string id, string threadId)> GetKeys(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string sql =
            """
             SELECT
               c.id,
               c.threadId
             FROM 
               c
            """;
        var queryDefinition = new QueryDefinition(sql);
        using var feedIterator = _client
            .GetDatabase(_options.DatabaseName)
            .GetContainer(_options.ContainerName)
            .GetItemQueryIterator<CosmosChatHistoryKey>(queryDefinition);

        while (feedIterator.HasMoreResults)
        {
            var response = await feedIterator.ReadNextAsync(cancellationToken);
            foreach (var value in response) yield return (value.Id, value.ThreadId);
        }
    }

    private async Task<CosmosChatHistory?> GetChatHistory(
        Container container,
        string sessionId,
        string threadId,
        CancellationToken cancellationToken)
    {
        var response = await container.ReadItemStreamAsync(
            sessionId,
            new PartitionKey(threadId),
            cancellationToken: cancellationToken);

        if (response.IsSuccessStatusCode)
            return _client.ClientOptions.Serializer.FromStream<CosmosChatHistory>(response.Content);

        _logger.LogDebug("Read {Container} {Id} {ThreadId} response {Status}",
            _options.ContainerName,
            sessionId,
            threadId,
            response.StatusCode);
        return default;
    }
}