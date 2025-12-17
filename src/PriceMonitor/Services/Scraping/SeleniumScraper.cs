using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using PriceMonitor.Models;
using PriceMonitor.Settings;
using SeleniumExtras.WaitHelpers;

namespace PriceMonitor.Services.Scraping;

public class SeleniumScraper : ISeleniumScraper
{
    private readonly ScraperSettings _settings;

    public SeleniumScraper(IOptionsSnapshot<ScraperSettings> options)
    {
        _settings = options.Value;
    }

    public async Task<ScrapeResult> ExecuteAsync(Product product, WebsiteRule rule, CancellationToken cancellationToken)
    {
        var options = new ChromeOptions();
        if (_settings.Headless)
        {
            options.AddArgument("--headless=new");
        }
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");

        try
        {
            using var driver = new ChromeDriver(options);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(rule.TimeoutSeconds ?? _settings.NavigationTimeoutSeconds);

            var url = BuildSearchUrl(product, rule);
            var delay = rule.RequestDelayMs ?? _settings.DefaultDelayMs;
            if (delay > 0)
            {
                await Task.Delay(delay, cancellationToken);
            }

            driver.Navigate().GoToUrl(url);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(rule.TimeoutSeconds ?? _settings.NavigationTimeoutSeconds));

            var priceElement = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(rule.PriceSelector)));
            var titleElement = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(rule.TitleSelector)));

            var price = ExtractPrice(priceElement.Text, rule.PriceRegex);
            var availability = TryFindText(driver, rule.AvailabilitySelector);
            var evidence = TryFindHtml(driver, rule.EvidenceSelector ?? rule.PriceSelector);

            var confidence = price.HasValue ? 0.8 : 0.1;

            return new ScrapeResult(
                Success: price.HasValue,
                Title: titleElement.Text,
                Price: price ?? 0,
                Currency: rule.Currency ?? "IRR",
                Availability: availability,
                Confidence: confidence,
                EvidenceHtml: evidence,
                ErrorMessage: price.HasValue ? null : "Price not found"
            );
        }
        catch (Exception ex)
        {
            return new ScrapeResult(false, null, null, rule.Currency ?? "IRR", null, 0, null, ex.Message);
        }
    }

    private static string BuildSearchUrl(Product product, WebsiteRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.SearchUrlTemplate))
        {
            return rule.SearchUrlTemplate
                .Replace("{sku}", Uri.EscapeDataString(product.Sku ?? string.Empty))
                .Replace("{name}", Uri.EscapeDataString(product.Name));
        }

        return rule.BaseUrl;
    }

    private static decimal? ExtractPrice(string raw, string pattern)
    {
        var match = Regex.Match(raw, pattern);
        if (!match.Success) return null;
        if (decimal.TryParse(match.Value.Replace(",", string.Empty), out var price))
        {
            return price;
        }

        return null;
    }

    private static string? TryFindText(IWebDriver driver, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        try
        {
            return driver.FindElement(By.CssSelector(selector)).Text;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryFindHtml(IWebDriver driver, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        try
        {
            return driver.FindElement(By.CssSelector(selector)).GetAttribute("innerHTML");
        }
        catch
        {
            return null;
        }
    }
}
