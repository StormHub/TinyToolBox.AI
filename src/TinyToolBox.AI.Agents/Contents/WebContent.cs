using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace TinyToolBox.AI.Agents.Contents;

internal sealed class WebContent : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly IBrowserContext _browserContext;
    private readonly ILogger _logger;
    
    private WebContent(
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

    public static async Task<WebContent> Create(ILogger logger)
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
        
        return new WebContent(playwright, browser, browserContext, logger);
    }

    public async Task<string?> GetPageContent(Uri uri)
    {
        var url = uri.ToString();
        if (url.EndsWith("pdf", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Not supported content type {Url}", uri);
            return default;
        }
        
        // Default html
        HtmlContent? pageContent = default;
        try
        {
            pageContent = await GotoHtmlPage(url);
            if (pageContent is not null)
            {
                return await pageContent.Text();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to get {Url}", uri);
        }
        finally
        {
            if (pageContent is not null)
            {
                await pageContent.DisposeAsync();
            }
        }

        return default;
    }

    private async Task<HtmlContent?> GotoHtmlPage(string url)
    {
        var page = await _browserContext.NewPageAsync();
        var response = await page.GotoAsync(
            url: url,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });
        if (response is null || !response.Ok)
        {
            _logger.LogError("Unable to get content of {Url} {Status}", url, response?.StatusText);
            return default;
        }

        _logger.LogInformation("{Url} DOMContentLoaded", url);
        return new HtmlContent(page);
    }

    public async ValueTask DisposeAsync()
    {
        await _browserContext.DisposeAsync();
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}