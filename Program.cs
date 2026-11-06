using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SmartMonitoringSolutions.Data;
using SmartMonitoringSolutions.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// --- Azure SQL Database (Users / DeviceRegistry) via EF Core, Entra ID authentication ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Simple cookie-based authentication backed by the Users table ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// --- Azure Storage (Blob + Table) options and services ---
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<PasswordHasherService>();
builder.Services.AddScoped<BlobStorageService>();
builder.Services.AddScoped<TelemetryTableService>();

// --- HTTP client for the serverless Functions pipeline (GET telemetry / POST irrigation) ---
builder.Services.Configure<FunctionsApiOptions>(builder.Configuration.GetSection("FunctionsApi"));
builder.Services.AddHttpClient<FunctionsApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FunctionsApiOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl);
    }

    var internalApiKey = builder.Configuration["InternalApiKey"];
    if (!string.IsNullOrWhiteSpace(internalApiKey))
    {
        client.DefaultRequestHeaders.Add("x-internal-api-key", internalApiKey);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await DbInitializer.InitializeAsync(app.Services);

app.Run();
