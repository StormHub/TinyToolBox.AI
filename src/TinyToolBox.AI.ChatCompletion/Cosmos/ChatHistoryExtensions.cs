using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using AudioContent = Microsoft.SemanticKernel.AudioContent;
using FunctionCallContent = Microsoft.SemanticKernel.FunctionCallContent;
using FunctionResultContent = Microsoft.SemanticKernel.FunctionResultContent;
using ImageContent = Microsoft.SemanticKernel.ImageContent;
using TextContent = Microsoft.SemanticKernel.TextContent;

// ReSharper disable RedundantNameQualifier

namespace TinyToolBox.AI.ChatCompletion.Cosmos;

internal static class ChatHistoryExtensions
{
    /// Source from https://github.com/microsoft/semantic-kernel/blob/main/dotnet/src/SemanticKernel.Abstractions/AI/ChatCompletion/ChatCompletionServiceExtensions.cs
    /// <summary>Converts a <see cref="ChatMessageContent" /> to a <see cref="ChatMessage" />.</summary>
    /// <remarks>This conversion should not be necessary once SK eventually adopts the shared content types.</remarks>
    internal static ChatMessage ToChatMessage(ChatMessageContent content)
    {
        ChatMessage message = new()
        {
            AdditionalProperties = content.Metadata is not null ? new(content.Metadata) : null,
            AuthorName = content.AuthorName,
            RawRepresentation = content.InnerContent,
            Role = content.Role.Label is string label ? new ChatRole(label) : ChatRole.User
        };

        foreach (var item in content.Items)
        {
            AIContent? aiContent = null;
            switch (item)
            {
                case TextContent tc:
                    aiContent = new Microsoft.Extensions.AI.TextContent(tc.Text);
                    break;

                case ImageContent ic:
                    aiContent =
                        ic.DataUri is not null ? new Microsoft.Extensions.AI.ImageContent(ic.DataUri, ic.MimeType) :
                        ic.Uri is not null ? new Microsoft.Extensions.AI.ImageContent(ic.Uri, ic.MimeType) :
                        null;
                    break;

                case AudioContent ac:
                    aiContent =
                        ac.DataUri is not null ? new Microsoft.Extensions.AI.AudioContent(ac.DataUri, ac.MimeType) :
                        ac.Uri is not null ? new Microsoft.Extensions.AI.AudioContent(ac.Uri, ac.MimeType) :
                        null;
                    break;

                case Microsoft.SemanticKernel.BinaryContent bc:
                    aiContent =
                        bc.DataUri is not null ? new Microsoft.Extensions.AI.DataContent(bc.DataUri, bc.MimeType) :
                        bc.Uri is not null ? new Microsoft.Extensions.AI.DataContent(bc.Uri, bc.MimeType) :
                        null;
                    break;

                case FunctionCallContent fcc:
                    aiContent = new Microsoft.Extensions.AI.FunctionCallContent(fcc.Id ?? string.Empty,
                        fcc.FunctionName,
                        fcc.Arguments);
                    break;

                case FunctionResultContent frc:
                    aiContent = new Microsoft.Extensions.AI.FunctionResultContent(frc.CallId ?? string.Empty,
                        frc.FunctionName ?? string.Empty,
                        frc.Result);
                    break;
            }

            if (aiContent is not null)
            {
                aiContent.RawRepresentation = item.InnerContent;
                aiContent.AdditionalProperties = item.Metadata is not null ? new(item.Metadata) : null;

                message.Contents.Add(aiContent);
            }
        }

        return message;
    }

    /// Source from https://github.com/microsoft/semantic-kernel/blob/main/dotnet/src/SemanticKernel.Abstractions/AI/ChatCompletion/ChatCompletionServiceExtensions.cs
    /// <summary>Converts a <see cref="ChatMessage" /> to a <see cref="ChatMessageContent" />.</summary>
    /// <remarks>This conversion should not be necessary once SK eventually adopts the shared content types.</remarks>
    internal static ChatMessageContent ToChatMessageContent(
        ChatMessage message,
        Microsoft.Extensions.AI.ChatCompletion? completion = null)
    {
        ChatMessageContent result = new()
        {
            ModelId = completion?.ModelId,
            AuthorName = message.AuthorName,
            InnerContent = completion?.RawRepresentation ?? message.RawRepresentation,
            Metadata = message.AdditionalProperties,
            Role = new AuthorRole(message.Role.Value)
        };

        foreach (var content in message.Contents)
        {
            KernelContent? resultContent = null;
            switch (content)
            {
                case Microsoft.Extensions.AI.TextContent tc:
                    resultContent = new TextContent(tc.Text);
                    break;

                case Microsoft.Extensions.AI.ImageContent ic:
                    resultContent = ic.ContainsData ? new ImageContent(ic.Uri) : new ImageContent(new Uri(ic.Uri));
                    break;

                case Microsoft.Extensions.AI.AudioContent ac:
                    resultContent = ac.ContainsData ? new AudioContent(ac.Uri) : new AudioContent(new Uri(ac.Uri));
                    break;

                case Microsoft.Extensions.AI.DataContent dc:
                    resultContent = dc.ContainsData
                        ? new Microsoft.SemanticKernel.BinaryContent(dc.Uri)
                        : new Microsoft.SemanticKernel.BinaryContent(new Uri(dc.Uri));
                    break;

                case Microsoft.Extensions.AI.FunctionCallContent fcc:
                    resultContent = new FunctionCallContent(fcc.Name,
                        null,
                        fcc.CallId,
                        fcc.Arguments is not null ? new(fcc.Arguments) : null);
                    break;

                case Microsoft.Extensions.AI.FunctionResultContent frc:
                    resultContent = new FunctionResultContent(frc.Name, null, frc.CallId, frc.Result);
                    break;
            }

            if (resultContent is not null)
            {
                resultContent.Metadata = content.AdditionalProperties;
                resultContent.InnerContent = content.RawRepresentation;
                resultContent.ModelId = completion?.ModelId;
                result.Items.Add(resultContent);
            }
        }

        return result;
    }
}