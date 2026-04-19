using ComfyBlazorApp.Components;
using ComfyBlazorApp.Data;
using ComfyBlazorApp.Options;
using ComfyBlazorApp.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor ──────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── ComfyUI options + HTTP client ────────────────────────────────────────────
builder.Services.Configure<ComfyUiOptions>(builder.Configuration.GetSection("ComfyUI"));
builder.Services.AddHttpClient<ComfyUiService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ComfyUiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

// ── Existing singletons ───────────────────────────────────────────────────────
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<JobService>();
builder.Services.AddHostedService<ComfyUiWebSocketService>();

// ── EF Core / SQLite ─────────────────────────────────────────────────────────
var dbPath = builder.Configuration.GetValue<string>("Database:Path")
    ?? Path.Combine(builder.Environment.ContentRootPath, "data", "prompts.db");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// ── New services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<PromptService>();
builder.Services.AddScoped<BatchService>();
builder.Services.AddScoped<SourceImageService>();
builder.Services.AddScoped<PromptHistoryService>();
builder.Services.AddSingleton<BatchWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BatchWorker>());

var app = builder.Build();

// ── Auto-migrate on startup ────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ── Middleware ────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

// ── Static files ──────────────────────────────────────────────────────────────
app.MapStaticAssets();

// Serve ComfyUI output images
app.MapGet("/images/{fileName}", (string fileName, ImageService imageService) =>
{
    var path = imageService.TryGetImagePath(fileName);
    if (path is null) return Results.NotFound();

    var provider = new FileExtensionContentTypeProvider();
    if (!provider.TryGetContentType(path, out var contentType))
        contentType = "application/octet-stream";

    return Results.File(path, contentType);
});

// Serve source images (decoupled from presets)
app.MapGet("/source-images/{fileName}", (string fileName, IWebHostEnvironment env) =>
{
    var dir = Path.Combine(env.ContentRootPath, "data", "source-images");
    var path = Path.GetFullPath(Path.Combine(dir, Path.GetFileName(fileName)));
    if (!path.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase))
        return Results.NotFound();
    if (!File.Exists(path)) return Results.NotFound();

    var provider = new FileExtensionContentTypeProvider();
    if (!provider.TryGetContentType(path, out var contentType))
        contentType = "application/octet-stream";

    return Results.File(path, contentType);
});

// Serve preset thumbnail images
app.MapGet("/preset-images/{fileName}", (string fileName, IWebHostEnvironment env) =>
{
    var dir = Path.Combine(env.ContentRootPath, "data", "prompt-images");
    var path = Path.GetFullPath(Path.Combine(dir, Path.GetFileName(fileName)));
    if (!path.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase))
        return Results.NotFound();
    if (!File.Exists(path)) return Results.NotFound();

    var provider = new FileExtensionContentTypeProvider();
    if (!provider.TryGetContentType(path, out var contentType))
        contentType = "application/octet-stream";

    return Results.File(path, contentType);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
