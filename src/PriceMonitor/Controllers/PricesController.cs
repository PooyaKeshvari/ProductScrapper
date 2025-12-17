using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceMonitor.Data;

namespace PriceMonitor.Controllers;

public class PricesController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public PricesController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var prices = await _dbContext.Prices
            .Include(p => p.Product)
            .OrderByDescending(p => p.RetrievedAt)
            .Take(200)
            .ToListAsync();
        return View(prices);
    }
}
