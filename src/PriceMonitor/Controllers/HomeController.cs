using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceMonitor.Data;

namespace PriceMonitor.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public HomeController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var productCount = await _dbContext.Products.CountAsync();
        var priceCount = await _dbContext.Prices.CountAsync();
        var runCount = await _dbContext.Runs.CountAsync();

        ViewBag.ProductCount = productCount;
        ViewBag.PriceCount = priceCount;
        ViewBag.RunCount = runCount;
        return View();
    }
}
