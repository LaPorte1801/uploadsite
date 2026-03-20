using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using UploadSite.Web.Data;
using UploadSite.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var appDataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
var databasePath = Environment.GetEnvironmentVariable("UPLOADSITE_DB_PATH")
    ?? Path.Combine(appDataPath, "uploadsite.db");
var dataProtectionPath = Environment.GetEnvironmentVariable("UPLOADSITE_KEYS_PATH")
    ?? Path.Combine(appDataPath, "keys");
var stagingRoot = Environment.GetEnvironmentVariable("UPLOADSITE_STAGING_ROOT")
    ?? Path.Combine(builder.Environment.ContentRootPath, "storage", "staging");
var libraryRoot = Environment.GetEnvironmentVariable("UPLOADSITE_LIBRARY_ROOT")
    ?? Path.Combine(builder.Environment.ContentRootPath, "storage", "library");
var seedOptions = new SeedOptions
{
    AdminUserName = Environment.GetEnvironmentVariable("UPLOADSITE_ADMIN_USERNAME") ?? "admin",
    AdminPassword = Environment.GetEnvironmentVariable("UPLOADSITE_ADMIN_PASSWORD") ?? "ChangeMeNow!"
};

builder.Services.AddSingleton(new AppPaths
{
    DatabasePath = databasePath,
    StagingRoot = stagingRoot,
    LibraryRoot = libraryRoot
});
builder.Services.AddSingleton(seedOptions);
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("UploadSite");
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAppSeeder, AppSeeder>();
builder.Services.AddScoped<IAudioMetadataService, AudioMetadataService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "uploadsite.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.MaxAge = TimeSpan.FromDays(30);
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024L * 1024L * 1024L;
    options.MultipartHeadersLengthLimit = 64 * 1024;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024L * 1024L * 1024L;
});
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IAppSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Upload}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapGet("/error", () => Results.Problem("An unexpected error occurred."));

app.Run();
