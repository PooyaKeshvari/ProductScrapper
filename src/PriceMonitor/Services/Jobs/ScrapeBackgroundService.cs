using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PriceMonitor.Data;
using PriceMonitor.Settings;
using PriceMonitor.Services.Scraping;

namespace PriceMonitor.Services.Jobs;

public class ScrapeBackgroundService : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly ScraperSettings _settings;
    private readonly ILogger<ScrapeBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(30);

    public ScrapeBackgroundService(IServiceProvider provider, IOptionsSnapshot<ScraperSettings> settings, ILogger<ScrapeBackgroundService> logger)
    {
        _provider = provider;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunBatchAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    public async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IScrapeOrchestrator>();

        var products = await db.Products.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var product in products)
        {
            try
            {
                await orchestrator.RunForProductAsync(product, _settings.Websites, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed scraping for product {ProductId}", product.Id);
            }
        }
    }
}
