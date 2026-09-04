using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PAYLO_WEB.Auth;
using PAYLO_WEB.Models;
using PAYLO_WEB.Services;

var builder = WebApplication.CreateBuilder(args);

// ?? Forwarded headers — Cloudflare + reverse proxy real IP ???????????????
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ?? Data Protection — persist keys so antiforgery survives IIS restart ???
// Without this: IIS restart resets keys ? antiforgery tokens invalid ? 400

// ? Stores keys inside the app's working directory
// Path auto-resolves regardless of server location
var keysFolder = Path.Combine(
    builder.Environment.ContentRootPath,   // app root folder
    "DataProtectionKeys");

Directory.CreateDirectory(keysFolder);     // creates if not exists

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("VedalexNepalApp");

//builder.Services.AddDataProtection()
//    .PersistKeysToFileSystem(
//        new DirectoryInfo(@"C:\DataProtectionKeys\VedalexNepal"))
//    .SetApplicationName("VedalexNepalApp");   // ? isolates from vedalex.in

// ?? Antiforgery — unique cookie name, isolates from India site ???????????
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = ".Vedalex.Nepal.AF";
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

var isDev = builder.Environment.IsDevelopment();

// ?? OpensiteClient — public pages, API key auth ???????????????????????????
builder.Services.AddHttpClient("OpensiteClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ApiUrl")!);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromMinutes(1);
    //client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler { AllowAutoRedirect = false };
    if (isDev)
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

// ? ApiClient — used by AuthService.SignOutAsync (was missing ? fallback client)
builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ApiUrl")!);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromMinutes(1);
    //client.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler { AllowAutoRedirect = false };
    if (isDev)
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

// ?? Cookie Authentication ?????????????????????????????????????????????????
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "AdminScheme";
    options.DefaultChallengeScheme = "AdminScheme";
});

foreach (var cfg in SchemeRegistry.AllConfigs)
{
    var schemeName = cfg.SchemeName;
    var loginPath = cfg.LoginPath;

    authBuilder.AddCookie(schemeName, options =>
    {
        options.LoginPath = loginPath;
        options.AccessDeniedPath = loginPath;

        // ? Nepal prefix — isolates from India vedalex.in cookies (same server)
        options.Cookie.Name = $".Vedalex.Nepal.{schemeName.Replace("Scheme", "")}";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = false;

        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var regId = context.Principal?.FindFirst("RegID")?.Value;
                var sessionId = context.Principal?.FindFirst("SessionId")?.Value;
                var userType = context.Principal?.FindFirst("UserType")?.Value;
                var expiry = context.Principal?.FindFirst("AccessTokenExpiry")?.Value;

                bool valid = !string.IsNullOrEmpty(regId)
                          && !string.IsNullOrEmpty(sessionId)
                          && !string.IsNullOrEmpty(userType)
                          && !string.IsNullOrEmpty(expiry)
                          && int.TryParse(regId, out var rid) && rid > 0
                          && SchemeRegistry.Get(userType)?.SchemeName == schemeName
                          && DateTime.TryParse(expiry, null,
                                 System.Globalization.DateTimeStyles.RoundtripKind,
                                 out var exp)
                          // ? 30s grace — lets logout complete at expiry boundary
                          && exp.ToUniversalTime() > DateTime.UtcNow.AddSeconds(-30);

                if (!valid)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(schemeName);
                }
            }
        };
    });
}

// ?? CommanHttpServices — authenticated controllers, JWT auth ?????????????
builder.Services.AddHttpClient<CommanHttpServices>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ApiUrl")!);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromMinutes(3);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler { AllowAutoRedirect = false };
    if (isDev)
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

builder.Services.Configure<ApiSettings>(
    builder.Configuration.GetSection("ApiSettings"));

builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// ? FIRST — resolve real IP before any middleware reads it
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{action=Login}/{id?}",
    defaults: new { controller = "Admin" });


// ============================================================
// ROOT ROUTE
// http://localhost:xxxx/
// Opens Admin Login
// ============================================================
app.MapControllerRoute(
    name: "root",
    pattern: "",
    defaults: new
    {
        area = "Admin",
        controller = "Admin",
        action = "Login"
    });

app.Run();