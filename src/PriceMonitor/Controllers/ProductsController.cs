using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceMonitor.Data;
using PriceMonitor.Models;
using PriceMonitor.Services.Pricing;

namespace PriceMonitor.Controllers;

public class ProductsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPricingService _pricing;

    public ProductsController(ApplicationDbContext dbContext, IPricingService pricing)
    {
        _dbContext = dbContext;
        _pricing = pricing;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _dbContext.Products.AsNoTracking().ToListAsync();
        return View(products);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _dbContext.Products
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null) return NotFound();

        var ranking = _pricing.Rank(product.InternalPrice, product.Prices);
        ViewBag.Ranking = ranking;
        return View(product);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product)
    {
        if (!ModelState.IsValid) return View(product);
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _dbContext.Products.FindAsync(id);
        if (product == null) return NotFound();
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product product)
    {
        if (id != product.Id) return BadRequest();
        if (!ModelState.IsValid) return View(product);

        product.UpdatedAt = DateTime.UtcNow;
        _dbContext.Update(product);
        await _dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var product = await _dbContext.Products.FindAsync(id);
        if (product == null) return NotFound();
        return View(product);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var product = await _dbContext.Products.FindAsync(id);
        if (product != null)
        {
            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
