# Price Monitoring Robot Architecture (ASP.NET Core MVC)

## High-Level Architecture
```
+---------------------------------------------------------------+
|                          MVC Web App                          |
|  (Admin UI + REST endpoints for control/observability)        |
+---------------------------------------------------------------+
| Config & Runtime Control | Background Job Orchestrator        |
|   - appsettings.json     | - Hangfire-like in-process         |
|   - UI-based editor      |   scheduler using IHostedService  |
+--------------------------+------------------------------------+
| Scraping Layer (Selenium Workers)                             |
| - Website-specific rules from config                          |
| - Request pacing & retries (backoff)                          |
+---------------------------------------------------------------+
| Matching & Validation Layer                                   |
| - Title/brand/attribute matching                              |
| - Anti-fake-price heuristics                                  |
+---------------------------------------------------------------+
| Pricing & Comparison Layer                                    |
| - Normalization (currency, tax)                               |
| - Market average, rank, deltas                                |
+---------------------------------------------------------------+
| Persistence Layer                                             |
| - EF Core (SQL Server/SQLite)                                 |
| - Tables: Products, Websites, Runs, ScrapeResults, Prices,    |
|   PriceHistory, Logs, ConfigurationSnapshots                  |
+---------------------------------------------------------------+
| External Sites (Iranian e-commerce targets)                   |
+---------------------------------------------------------------+
```

### Why Each Component Exists
- **Config & Runtime Control**: All scraping rules are runtime-configurable (selectors, regex, delays, enable/disable). Prevents redeploys for site changes.
- **Background Job Orchestrator**: Sequential/limited-parallel execution to respect target sites and avoid bans; runs via `IHostedService`.
- **Scraping Layer**: Isolates Selenium logic (headless, retries, pacing) and keeps selectors outside code.
- **Matching & Validation Layer**: Prevents false positives through structured product comparison and confidence scoring.
- **Pricing & Comparison Layer**: Normalizes prices, computes market averages/ranks, and applies anti-fake rules.
- **Persistence Layer**: Durable storage of runs, histories, evidence (HTML snippets), and configuration snapshots.
- **Dashboard/UI Layer**: CRUD for products/websites, view runs & logs, edit config safely.

## Folder Structure (ASP.NET Core MVC)
```
ProductScrapper/
├─ src/
│  ├─ ProductScrapper.Web/            # ASP.NET Core MVC project
│  │  ├─ Controllers/
│  │  │  ├─ ProductsController.cs
│  │  │  ├─ WebsitesController.cs
│  │  │  ├─ RunsController.cs
│  │  │  ├─ PricesController.cs
│  │  │  ├─ ConfigController.cs       # Runtime config editor
│  │  ├─ Views/ (Bootstrap only)
│  │  ├─ Jobs/
│  │  │  ├─ ScrapeScheduler.cs        # IHostedService
│  │  │  ├─ ScrapeExecutor.cs
│  │  ├─ Scraping/
│  │  │  ├─ SeleniumScraper.cs
│  │  │  ├─ IScraper.cs
│  │  │  ├─ WebsiteRule.cs
│  │  ├─ Matching/
│  │  │  ├─ IProductMatcher.cs
│  │  │  ├─ FuzzyProductMatcher.cs
│  │  ├─ Pricing/
│  │  │  ├─ PriceNormalizer.cs
│  │  │  ├─ PriceComparer.cs
│  │  ├─ Validation/
│  │  │  ├─ PriceValidator.cs
│  │  ├─ Data/
│  │  │  ├─ AppDbContext.cs
│  │  │  ├─ Entities/*.cs
│  │  │  ├─ ConfigSnapshotService.cs
│  │  ├─ Services/
│  │  │  ├─ ScrapeService.cs
│  │  │  ├─ RunService.cs
│  │  │  ├─ LoggingService.cs
│  │  ├─ Models/ (ViewModels + DTOs)
│  │  ├─ appsettings.json
│  └─ ProductScrapper.Tests/          # Unit + integration tests
└─ docs/                              # Architecture and design notes
```

## Data Models & Tables (EF Core)
- **Product**: `Id`, `Sku`, `Name`, `Brand`, `AttributesJson`, `IsActive`.
- **Website**: `Id`, `Name`, `BaseUrl`, `IsEnabled`, `RequestDelayMs`, `SelectorsJson`.
- **ScrapeRun**: `Id`, `StartedAt`, `FinishedAt`, `Status`, `ProductCount`, `WebsiteCount`, `ErrorSummary`.
- **ScrapeTask**: `Id`, `ScrapeRunId`, `ProductId`, `WebsiteId`, `Status`, `Attempt`, `NextRunAt`.
- **PriceObservation**: `Id`, `ProductId`, `WebsiteId`, `ObservedAt`, `PriceRaw`, `PriceValue`, `Currency`, `Availability`, `Confidence`, `EvidenceHtml`, `SelectorVersion`.
- **PriceHistory**: `Id`, `ProductId`, `WebsiteId`, `PriceValue`, `ObservedAt` (aggregated from observations with acceptable confidence).
- **ComparisonSnapshot**: `Id`, `ProductId`, `ScrapeRunId`, `MarketAverage`, `MinPrice`, `MaxPrice`, `Rank`, `CompetitorCount`.
- **LogEntry**: `Id`, `Timestamp`, `Level`, `Category`, `Message`, `Exception`, `ContextJson`.
- **ConfigurationSnapshot**: `Id`, `TakenAt`, `ContentJson`, `Hash` (audit of runtime edits).

## Key Interfaces & Classes (C# Signatures)
```csharp
// Scraping layer
public interface IScraper
{
    Task<ScrapeResult> ScrapeAsync(Product product, WebsiteRule rule, CancellationToken ct);
}

public class SeleniumScraper : IScraper { /* uses WebDriverFactory */ }

public record WebsiteRule
{
    string Name { get; init; }
    string BaseUrl { get; init; }
    string SearchPathTemplate { get; init; } // e.g., "/search?q={query}"
    bool Enabled { get; init; }
    int RequestDelayMs { get; init; }
    SelectorConfig Selectors { get; init; }
    RegexConfig Regex { get; init; }
    int MaxRetries { get; init; }
    int RetryBackoffMs { get; init; }
}

public record SelectorConfig
{
    string TitleCss { get; init; }
    string PriceCss { get; init; }
    string AvailabilityCss { get; init; }
    string ProductLinkCss { get; init; }
}

public record RegexConfig
{
    string PricePattern { get; init; } // e.g., "([0-9.,]+)"
}

public record ScrapeResult
{
    string Title { get; init; }
    string PriceRaw { get; init; }
    decimal? PriceValue { get; init; }
    string Currency { get; init; }
    string Availability { get; init; }
    string EvidenceHtml { get; init; }
    double Confidence { get; init; }
    ScrapeStatus Status { get; init; }
    string Error { get; init; }
}

// Matching & validation
public interface IProductMatcher
{
    MatchResult Match(Product internalProduct, ScrapeResult scraped, WebsiteRule rule);
}

public record MatchResult(bool IsMatch, double Confidence, string Reason);

public class PriceValidator
{
    public ValidationResult Validate(ScrapeResult result, Product product, WebsiteRule rule);
}

public record ValidationResult(bool IsValid, double Confidence, string Reason);

// Pricing & comparison
public class PriceNormalizer
{
    public decimal? Normalize(string rawPrice, string currency, RegexConfig regex);
}

public class PriceComparer
{
    public ComparisonSnapshot CalculateSnapshot(Product product, IEnumerable<PriceObservation> prices);
}

// Services
public class ScrapeService
{
    Task<ScrapeRun> StartRunAsync(IEnumerable<int> productIds, CancellationToken ct);
    Task ExecuteTaskAsync(ScrapeTask task, CancellationToken ct);
}

public class ConfigSnapshotService
{
    Task<ConfigurationSnapshot> CaptureAsync();
    Task RestoreAsync(ConfigurationSnapshot snapshot);
}

// Background job host
public class ScrapeScheduler : IHostedService { /* schedules tasks sequentially or limited parallel */ }
```

## Selenium Scraping Pseudocode
```csharp
async Task<ScrapeResult> ScrapeAsync(Product product, WebsiteRule rule, CancellationToken ct)
{
    var driver = _webDriverFactory.Create(headless: true);
    try
    {
        var searchUrl = rule.BaseUrl + rule.SearchPathTemplate.Replace("{query}", UrlEncode(product.Name));
        await PaceAsync(rule.RequestDelayMs, ct);
        driver.Navigate().GoToUrl(searchUrl);

        var priceElement = SafeFind(driver, rule.Selectors.PriceCss);
        var titleElement = SafeFind(driver, rule.Selectors.TitleCss);
        var availElement = SafeFind(driver, rule.Selectors.AvailabilityCss);

        var priceRaw = ExtractText(priceElement);
        var priceValue = _priceNormalizer.Normalize(priceRaw, "IRR", rule.Regex);

        var validation = _priceValidator.Validate(new ScrapeResult { ... }, product, rule);
        if (!validation.IsValid) return new ScrapeResult { Status = ScrapeStatus.Invalid, Confidence = validation.Confidence, Error = validation.Reason };

        return new ScrapeResult
        {
            Title = titleElement?.Text,
            PriceRaw = priceRaw,
            PriceValue = priceValue,
            Currency = "IRR",
            Availability = availElement?.Text,
            EvidenceHtml = CaptureSnippet(driver.PageSource, priceElement),
            Confidence = validation.Confidence,
            Status = ScrapeStatus.Success
        };
    }
    catch (WebDriverException ex) when (IsLayoutChange(ex))
    {
        return new ScrapeResult { Status = ScrapeStatus.LayoutChanged, Error = ex.Message, Confidence = 0 };
    }
    catch (Exception ex)
    {
        return new ScrapeResult { Status = ScrapeStatus.Error, Error = ex.Message, Confidence = 0 };
    }
    finally
    {
        driver.Quit();
    }
}
```

**Retry with backoff**: the scheduler wraps `ScrapeAsync` in retry policy (max `rule.MaxRetries`) with exponential backoff and jitter, respecting `RequestDelayMs` between attempts.

## Matching Logic
- **Normalization**: Normalize strings (remove punctuation, normalize Persian/Arabic characters, lowercase, strip whitespace).
- **Key tokens**: Compare brand + model + capacity/size tokens extracted from `Product.AttributesJson` against scraped title.
- **Levenshtein/Jaro threshold**: `FuzzyProductMatcher` computes similarity; require both brand match and high token overlap (e.g., ≥0.8) to accept.
- **SKU/GTIN check**: If site exposes SKU/GTIN and matches ours, mark `Confidence = 1.0`.
- **Invalid match criteria**: Missing required brand token, conflicting attributes (e.g., different capacity), or similarity below threshold => `IsMatch = false`.
- **Fallback**: If no confident match, store null price with status `Unmatched` instead of guessing.

## Anti-Fake Price Strategy
- **Validation rules**:
  - Price must parse with regex; reject if formatted text contains suspicious words ("تومان قدیم", "تست", "%").
  - Reject prices with extreme deviation (e.g., >3x median of last 7 days or <0.3x) unless availability indicates sale; lower confidence otherwise.
  - Require availability status to be "in stock" or similar; otherwise mark as `OutOfStock` with low confidence.
  - Cross-check title contains brand/model tokens; penalize mismatch.
- **Evidence storage**: Save `EvidenceHtml` (sanitized snippet around price/title) and DOM path to aid manual review.
- **Confidence scoring**: Weighted factors: title match (0.4), regex parse quality (0.2), availability (0.1), historical consistency (0.2), selector freshness (0.1).
- **Fallback**: If confidence < 0.5, do not record in `PriceHistory`; keep `PriceObservation` with status `Rejected` and null normalized price.

## Pricing & Comparison
- **Normalization**: Convert all prices to IRR; support currency overrides if any site uses Toman (apply x10). Remove separators and apply regex pattern.
- **Market average**: Average of valid `PriceHistory` values in the last configurable window (e.g., 14 days). Compute min/max and rank relative to competitors.
- **Ranking**: Sort competitor prices ascending; find our internal price position; store `ComparisonSnapshot` per product per run.

## Dashboard/UI (MVC)
- **Controllers & Views**:
  - `ProductsController`: CRUD for internal products + attributes.
  - `WebsitesController`: Enable/disable sites, edit selectors/rules from config snapshot.
  - `RunsController`: Start manual run, view run status, rerun failed tasks.
  - `PricesController`: View price history and comparison snapshots per product.
  - `ConfigController`: Edit `appsettings.json`-backed scraping rules with validation and preview; persists a snapshot before applying.
  - `LogsController`: Filter logs by level, product, site, run.
- **Pages**: Dashboard summary (active runs, failures), products list/detail, websites list/detail, run history, price charts (Chart.js optional but can be skipped if using plain tables), logs stream, config editor.
- **Security**: Simple cookie auth + role `Admin` for config edits.

## Configuration Strategy (appsettings.json)
- All selectors, regex, delays, enable flags live under `Scraping:Websites`.
- `IOptionsSnapshot` for runtime reload; `ConfigSnapshotService` saves/rolls back.
- No hard-coded selectors; code only refers to strongly typed POCOs mapped from config.

### Example `appsettings.json`
```json
{
  "Scraping": {
    "Global": {
      "Headless": true,
      "DefaultDelayMs": 4000,
      "MaxParallelism": 2
    },
    "Websites": [
      {
        "Name": "Digikala",
        "Enabled": true,
        "BaseUrl": "https://www.digikala.com",
        "SearchPathTemplate": "/search/?q={query}",
        "RequestDelayMs": 5000,
        "MaxRetries": 2,
        "RetryBackoffMs": 2000,
        "Selectors": {
          "TitleCss": "div.product-title h1",
          "PriceCss": "div.price span",
          "AvailabilityCss": "div.availability",
          "ProductLinkCss": "a.product-link"
        },
        "Regex": {
          "PricePattern": "([0-9٬,]+)"
        }
      },
      {
        "Name": "Bamilo",
        "Enabled": false,
        "BaseUrl": "https://www.example.com",
        "SearchPathTemplate": "/search?q={query}",
        "RequestDelayMs": 3000,
        "MaxRetries": 3,
        "RetryBackoffMs": 1500,
        "Selectors": {
          "TitleCss": "h1.title",
          "PriceCss": "span.price",
          "AvailabilityCss": "div.stock",
          "ProductLinkCss": "a.product"
        },
        "Regex": {
          "PricePattern": "([0-9.,]+)"
        }
      }
    ]
  }
}
```

## Execution Flow
1. **Product selected**: Run created with selected product IDs; configuration snapshot taken.
2. **Target websites resolved**: Load enabled sites from config; build tasks (product × site).
3. **Selenium execution**: Scheduler runs tasks sequentially or limited parallel. Each task paces requests and retries with backoff.
4. **Extraction**: Scraper uses site selectors and regex to get price/title/availability; captures HTML snippet.
5. **Validation**: Matching and anti-fake rules evaluate confidence; invalid results flagged/rejected.
6. **Persistence**: Store `PriceObservation` (always) and `PriceHistory` (only when confident). Update run/task status.
7. **Comparison result**: Pricing layer computes market average/min/max and rank; store `ComparisonSnapshot`.

## Error Handling & Logging
- **Central logging**: Serilog or built-in logger with sinks to DB table `LogEntry` and file. Correlate logs with `ScrapeRunId` and `ScrapeTaskId`.
- **Failure classification**: `ScrapeStatus` enum (`Success`, `Unmatched`, `Invalid`, `LayoutChanged`, `Timeout`, `Captcha`, `Error`).
- **Retry logic**: Policy wraps scraper; layout change/captcha triggers disable-site flag until admin review.
- **Manual re-run**: UI button in `RunsController` enqueues failed tasks with fresh snapshot.

## Selenium Requirements Handling
- **Headless**: Chromium/Firefox headless mode configurable via `Scraping:Global:Headless`.
- **Request delay**: `RequestDelayMs` per site; enforced before each navigation; jitter adds ±10% random.
- **Selectors**: Loaded from config; `SafeFind` returns null on changes and logs selector version.
- **Graceful failure**: Missing selectors => `LayoutChanged` status, no crash.
- **Retry/backoff**: Exponential with jitter using `rule.RetryBackoffMs` and `rule.MaxRetries`.
- **Minimal footprint**: Reuse driver options to disable images/JS if possible; small parallelism.

## Best Practices for Iranian E-commerce Sites
- Normalize Persian/Arabic digits and separators (`٬`, `،`, `.`) before parsing prices.
- Handle Toman vs Rial: detect keywords and multiply by 10 where needed; store normalized IRR.
- Consider right-to-left (RTL) layouts; selectors should not depend on direction-specific markup.
- Handle Jalali dates if sites display last-update timestamps; convert to Gregorian if used.
- Many sites lazy-load prices; enable minimal wait (explicit waits on price selector) instead of sleeps.
- Watch for CDN/Captcha after high frequency; respect higher `RequestDelayMs` and random jitter.
- Some sites show prices only after selecting color/variant; pick first available variant or skip with low confidence.

## Anti-Hallucination / Anti-Fake Behavior
- Never fabricate price: if selector missing or regex fails, return null price and `Invalid` status.
- Evidence snippet stored for any accepted observation; UI shows snippet for manual verification.
- Confidence gate (≥0.5) required to enter `PriceHistory` and influence comparison.
- Configurable thresholds (similarity, deviation multipliers) in `appsettings.json` to tune without deploy.

## Performance Constraints Handling
- Default max parallel tasks = 2; otherwise sequential to avoid bans.
- With 500 products × 10 sites, runs are chunked; scheduler processes in batches respecting delays.
- Reuse drivers per site when safe to avoid overhead, but recreate on severe errors.
