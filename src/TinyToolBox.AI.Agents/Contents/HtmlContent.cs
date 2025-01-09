using Microsoft.Playwright;

namespace TinyToolBox.AI.Agents.Contents;

internal sealed class HtmlContent : IAsyncDisposable
{
    private readonly IPage _page;

    public HtmlContent(IPage page)
    {
        _page = page;
    }

    public async Task<string?> Text()
    {
        var handle = await _page.QuerySelectorAsync("article")
                     ?? await _page.QuerySelectorAsync("main");
        if (handle is not null)
        {
            return await handle.TextContentAsync();
        }

        // "content", "main-content", "post-content"
        var elements = await _page.QuerySelectorAllAsync("content");
        if (elements.Count == 0)
        {
            elements = await _page.QuerySelectorAllAsync("main-content");
        }

        if (elements.Count == 0)
        {
            elements = await _page.QuerySelectorAllAsync("post-content");
        }

        var contents = elements.ToArray();
        if (contents.Length == 0)
        {
            return default;
        }

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

    public async ValueTask DisposeAsync() => await _page.CloseAsync();
}