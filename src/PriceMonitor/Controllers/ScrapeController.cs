using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PriceMonitor.Data;
using PriceMonitor.Services.Scraping;
using PriceMonitor.Settings;

namespace PriceMonitor.Controllers;

public class ScrapeController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IScrapeOrchestrator _orchestrator;
    private readonly ScraperSettings _settings;

    public ScrapeController(ApplicationDbContext dbContext, IScrapeOrchestrator orchestrator, IOptionsSnapshot<ScraperSettings> settings)
    {
        _dbContext = dbContext;
        _orchestrator = orchestrator;
        _settings = settings.Value;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(int productId)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return NotFound();

        await _orchestrator.RunForProductAsync(product, _settings.Websites, HttpContext.RequestAborted);
        TempData["Message"] = "Scrape triggered";
        return RedirectToAction("Details", "Products", new { id = productId });
    }
}
