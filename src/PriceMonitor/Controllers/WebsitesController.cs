using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PriceMonitor.Settings;

namespace PriceMonitor.Controllers;

public class WebsitesController : Controller
{
    private readonly IOptionsMonitor<ScraperSettings> _settings;
    private readonly IWebHostEnvironment _environment;

    public WebsitesController(IOptionsMonitor<ScraperSettings> settings, IWebHostEnvironment environment)
    {
        _settings = settings;
        _environment = environment;
    }

    public IActionResult Index()
    {
        return View(_settings.CurrentValue.Websites);
    }

    public IActionResult Edit()
    {
        var json = JsonSerializer.Serialize(_settings.CurrentValue, new JsonSerializerOptions { WriteIndented = true });
        return View(model: json);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(string rawJson)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<ScraperSettings>(rawJson);
            if (parsed == null)
            {
                ModelState.AddModelError(string.Empty, "Could not parse configuration");
                return View(model: rawJson);
            }

            var runtimePath = Path.Combine(_environment.ContentRootPath, "appsettings.runtime.json");
            var wrapper = new { ScraperSettings = parsed };
            var serialized = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(runtimePath, serialized);
            TempData["Message"] = "Configuration saved to appsettings.runtime.json";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model: rawJson);
        }
    }
}
