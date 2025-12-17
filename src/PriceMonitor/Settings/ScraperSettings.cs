namespace PriceMonitor.Settings;

public class ScraperSettings
{
    public int DefaultDelayMs { get; set; } = 2000;
    public int NavigationTimeoutSeconds { get; set; } = 30;
    public int MaxParallelWebsites { get; set; } = 1;
    public bool Headless { get; set; } = true;
    public List<WebsiteRule> Websites { get; set; } = new();
}

public class WebsiteRule
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string SearchUrlTemplate { get; set; } = string.Empty;
    public string? SearchInputSelector { get; set; }
    public string? SearchSubmitSelector { get; set; }
    public string? ResultContainerSelector { get; set; }
    public string? ResultItemSelector { get; set; }
    public string? ResultTitleSelector { get; set; }
    public string? ResultLinkSelector { get; set; }
    public double MinimumMatchScore { get; set; } = 0.45;
    public string PriceSelector { get; set; } = string.Empty;
    public string TitleSelector { get; set; } = string.Empty;
    public string AvailabilitySelector { get; set; } = string.Empty;
    public string PriceRegex { get; set; } = @"\d+";
    public int? RequestDelayMs { get; set; }
    public bool Enabled { get; set; } = true;
    public int? TimeoutSeconds { get; set; }
    public string? Currency { get; set; } = "IRR";
    public string? EvidenceSelector { get; set; }
}
