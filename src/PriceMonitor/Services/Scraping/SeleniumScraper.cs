using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using PriceMonitor.Models;
using PriceMonitor.Services.Matching;
using PriceMonitor.Settings;
using SeleniumExtras.WaitHelpers;

namespace PriceMonitor.Services.Scraping;

public class SeleniumScraper : ISeleniumScraper
{
    private readonly ScraperSettings _settings;
    private readonly IProductMatcher _matcher;

    public SeleniumScraper(IOptionsSnapshot<ScraperSettings> options, IProductMatcher matcher)
    {
        _settings = options.Value;
        _matcher = matcher;
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
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(rule.TimeoutSeconds ?? _settings.NavigationTimeoutSeconds));

            var delay = rule.RequestDelayMs ?? _settings.DefaultDelayMs;
            await NavigateToSearchAsync(driver, wait, product, rule, delay, cancellationToken);

            var (_, detailNavigationResult) = await FindAndOpenBestResultAsync(driver, wait, product, rule, delay, cancellationToken);
            if (!detailNavigationResult.Success)
            {
                return detailNavigationResult;
            }

            var priceElement = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(rule.PriceSelector)));
            var titleElement = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(rule.TitleSelector)));

            var price = ExtractPrice(priceElement.Text, rule.PriceRegex);
            var availability = TryFindText(driver, rule.AvailabilitySelector);
            var evidence = TryFindHtml(driver, rule.EvidenceSelector ?? rule.PriceSelector);

            var confidence = price.HasValue ? Math.Max(rule.MinimumMatchScore, 0.5) : rule.MinimumMatchScore / 2;
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

    private async Task NavigateToSearchAsync(IWebDriver driver, WebDriverWait wait, Product product, WebsiteRule rule, int delay, CancellationToken cancellationToken)
    {
        var url = BuildSearchUrl(product, rule);
        if (!string.IsNullOrWhiteSpace(url))
        {
            await Task.Delay(delay, cancellationToken);
            driver.Navigate().GoToUrl(url);
        }

        if (string.IsNullOrWhiteSpace(rule.SearchInputSelector))
        {
            return;
        }

        var searchInput = wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector(rule.SearchInputSelector)));
        searchInput.Clear();
        searchInput.SendKeys(product.Name);
        if (!string.IsNullOrWhiteSpace(product.Sku))
        {
            searchInput.SendKeys(" " + product.Sku);
        }

        if (!string.IsNullOrWhiteSpace(rule.SearchSubmitSelector))
        {
            var submitButton = wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(rule.SearchSubmitSelector)));
            submitButton.Click();
        }
        else
        {
            searchInput.SendKeys(Keys.Enter);
        }
    }

    private async Task<(string? Title, ScrapeResult Result)> FindAndOpenBestResultAsync(IWebDriver driver, WebDriverWait wait, Product product, WebsiteRule rule, int delay, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyCollection<IWebElement> resultItems = Array.Empty<IWebElement>();
            if (!string.IsNullOrWhiteSpace(rule.ResultItemSelector))
            {
                resultItems = wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.CssSelector(rule.ResultItemSelector)));
            }
            else if (!string.IsNullOrWhiteSpace(rule.ResultContainerSelector))
            {
                var container = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(rule.ResultContainerSelector)));
                resultItems = container.FindElements(By.CssSelector(rule.ResultTitleSelector ?? "*"));
            }

            var bestScore = 0d;
            IWebElement? bestItem = null;
            string? bestTitle = null;
            string? bestLink = null;

            foreach (var item in resultItems)
            {
                var titleText = TryFindChildText(item, rule.ResultTitleSelector);
                if (string.IsNullOrWhiteSpace(titleText))
                {
                    continue;
                }

                var score = _matcher.EvaluateMatch(product, titleText);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestItem = item;
                bestTitle = titleText;
                bestLink = TryFindChildLink(item, rule.ResultLinkSelector);
            }

            if (bestItem == null || bestScore < rule.MinimumMatchScore)
            {
                return (null, new ScrapeResult(false, null, null, rule.Currency ?? "IRR", null, bestScore, null, "No suitable result found"));
            }

            if (!string.IsNullOrWhiteSpace(bestLink))
            {
                await Task.Delay(delay, cancellationToken);
                driver.Navigate().GoToUrl(bestLink);
            }
            else
            {
                await Task.Delay(delay, cancellationToken);
                bestItem.Click();
            }

            return (bestTitle, new ScrapeResult(true, bestTitle, null, rule.Currency ?? "IRR", null, bestScore, null, null));
        }
        catch (Exception ex)
        {
            return (null, new ScrapeResult(false, null, null, rule.Currency ?? "IRR", null, 0, null, ex.Message));
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
        var cleaned = match.Value.Replace(",", string.Empty).Replace("٫", string.Empty);
        if (decimal.TryParse(cleaned, out var price))
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

    private static string? TryFindChildText(IWebElement parent, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return parent.Text;
        try
        {
            return parent.FindElement(By.CssSelector(selector)).Text;
        }
        catch
        {
            return parent.Text;
        }
    }

    private static string? TryFindChildLink(IWebElement parent, string? selector)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(selector))
            {
                return parent.FindElement(By.CssSelector(selector)).GetAttribute("href");
            }

            return parent.GetAttribute("href");
        }
        catch
        {
            return null;
        }
    }
}
