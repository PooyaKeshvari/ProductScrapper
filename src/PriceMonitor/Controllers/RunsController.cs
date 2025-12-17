using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceMonitor.Data;

namespace PriceMonitor.Controllers;

public class RunsController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public RunsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var runs = await _dbContext.Runs
            .Include(r => r.Product)
            .OrderByDescending(r => r.StartedAt)
            .Take(200)
            .ToListAsync();
        return View(runs);
    }
}
