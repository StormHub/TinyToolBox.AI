using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace TinyToolBox.AI.Agents.Browsers;

internal sealed class BrowserContext : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly IBrowserContext _browserContext;
    private readonly ILogger _logger;
    
    private BrowserContext(
        IPlaywright playwright, 
        IBrowser browser, 
        IBrowserContext browserContext, 
        ILogger logger)
    {
        _playwright = playwright;
        _browser = browser;
        _browserContext = browserContext;
        _logger = logger;
    }

    public static async Task<BrowserContext> Create(ILogger logger)
    {
        // var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var options = new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.102 Safari/537.36"
        };
        var browserContext = await browser.NewContextAsync(options);
        await browserContext.AddInitScriptAsync(
            """
            // Webdriver property
            Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined
            });

            // Languages
            Object.defineProperty(navigator, 'languages', {
                get: () => ['en-US', 'en']
            });

            // Plugins
            Object.defineProperty(navigator, 'plugins', {
                get: () => [1, 2, 3, 4, 5]
            });

            // Chrome runtime
            window.chrome = { runtime: {} };

            // Permissions
            const originalQuery = window.navigator.permissions.query;
            window.navigator.permissions.query = (parameters) => (
                parameters.name === 'notifications' ?
                    Promise.resolve({ state: Notification.permission }) :
                    originalQuery(parameters)
            );
            """);
        
        return new BrowserContext(playwright, browser, browserContext, logger);
    }

    public async Task<string?> GetPageContent(Uri uri)
    {
        IPage? page = default;
        try
        {
            page = await _browserContext.NewPageAsync();
            var response = await page.GotoAsync(uri.ToString());
            if (response is not null && response.Ok)
            {
                var handle = await page.QuerySelectorAsync("article")
                             ?? await page.QuerySelectorAsync("main");
                if (handle is not null)
                {
                    return await handle.TextContentAsync();
                }

                // "content", "main-content", "post-content"
                var elements = await page.QuerySelectorAllAsync("content");
                if (elements.Count == 0)
                {
                    elements = await page.QuerySelectorAllAsync("main-content");
                }
                if (elements.Count == 0)
                {
                    elements = await page.QuerySelectorAllAsync("post-content");
                }
                
                var contents = elements.ToArray();
                if (contents.Length > 0)
                {
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
            }
            else
            {
                _logger.LogError("Unable to get content of {Url} {Status}", uri, response?.StatusText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to get {Url}", uri);
        }
        finally
        {
            if (page is not null)
            {
                await page.CloseAsync();
            }
        }

        return default;
    }

    public async ValueTask DisposeAsync()
    {
        await _browserContext.DisposeAsync();
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}