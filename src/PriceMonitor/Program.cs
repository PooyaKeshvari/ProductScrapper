using Microsoft.EntityFrameworkCore;
using PriceMonitor.Data;
using PriceMonitor.Services.Jobs;
using PriceMonitor.Services.Matching;
using PriceMonitor.Services.Pricing;
using PriceMonitor.Services.Scraping;
using PriceMonitor.Services.Validation;
using PriceMonitor.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.runtime.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<ScraperSettings>(builder.Configuration.GetSection("ScraperSettings"));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=price-monitor.db"));

builder.Services.AddScoped<IScrapeOrchestrator, ScrapeOrchestrator>();
builder.Services.AddScoped<ISeleniumScraper, SeleniumScraper>();
builder.Services.AddScoped<IProductMatcher, ProductMatcher>();
builder.Services.AddScoped<IPriceValidator, PriceValidator>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddHostedService<ScrapeBackgroundService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
