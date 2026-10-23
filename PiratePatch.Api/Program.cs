using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Npgsql;
using PiratePatch.Api;
using PiratePatch.Api.Auth;
using PiratePatch.Api.Certificates;
using PiratePatch.Api.Challenge;
using PiratePatch.Api.Configuration;
using PiratePatch.Api.Data;
using PiratePatch.Api.Endpoints;
using PiratePatch.Api.Hosting;
using PiratePatch.Api.Notifications;
using PiratePatch.Api.Players;

var builder = WebApplication.CreateBuilder(args);

// systemd credentials (LoadCredential=) are files named like configuration keys, e.g. Telegram__BotToken.
if (Environment.GetEnvironmentVariable("CREDENTIALS_DIRECTORY") is { Length: > 0 } credentials)
    builder.Configuration.AddKeyPerFile(credentials, optional: true);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = 16 * 1024;
    kestrel.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
    kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
});

var services = builder.Services;

services.AddOptions<SiteOptions>().BindConfiguration(SiteOptions.Section).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<TelegramOptions>().BindConfiguration(TelegramOptions.Section).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<SigningOptions>().BindConfiguration(SigningOptions.Section).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.Section).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<AssetOptions>().BindConfiguration(AssetOptions.Section);
services.AddSingleton<IValidateOptions<SigningOptions>, SigningOptionsValidator>();

services.AddSingleton(sp => new SigningKey(sp.GetRequiredService<IOptions<SigningOptions>>().Value.DecodeSeed()));
services.AddSingleton<FlagCodec>();
services.AddSingleton<PasswordDeriver>();
services.AddSingleton(sp => ChallengeTemplate.Load(sp.GetRequiredService<IOptions<AssetOptions>>().Value.ChallengeTemplate));
services.AddSingleton<ChallengeBuilder>();
services.AddSingleton<CertificateRenderer>();
services.AddSingleton(sp =>
{
    var telegram = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
    return new TelegramLoginVerifier(telegram.BotToken, TimeSpan.FromSeconds(telegram.LoginMaxAgeSeconds), TimeProvider.System);
});

services.AddSingleton(sp => new NpgsqlDataSourceBuilder(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ToConnectionString()).Build());
services.AddSingleton<PlayerStore>();
services.AddHostedService<Migrator>();

services.AddHttpClient(TelegramNotifier.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
services.AddSingleton<TelegramNotifier>();
services.AddSingleton<IAdminNotifier>(sp => sp.GetRequiredService<TelegramNotifier>());
services.AddHostedService(sp => sp.GetRequiredService<TelegramNotifier>());

services.AddMemoryCache(cache => cache.SizeLimit = 16); // rendered certificates, about 400 KB each

services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    // Kestrel listens on a Unix socket only nginx can open, so there is no proxy address to pin
    // (a socket has no remote IP). nginx overwrites X-Forwarded-For; it is never client-supplied.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var site = builder.Configuration.GetSection(SiteOptions.Section).Get<SiteOptions>();
var devLogin = builder.Environment.IsDevelopment() && site?.DevLogin == true;

var dataProtection = services.AddDataProtection().SetApplicationName("piratepatch");
if (site?.KeysPath is { Length: > 0 } keysPath)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    // The __Host- prefix pins the cookie to this exact host over HTTPS, so it cannot be planted from a subdomain.
    options.Cookie.Name = builder.Environment.IsDevelopment() ? "pp_session" : "__Host-pp_session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(site?.SessionHours ?? 12);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
services.AddAuthorization();

services.AddPiratePatchRateLimiting();
services.AddValidation();
services.AddProblemDetails();

var app = builder.Build();

// Fail at startup, not on the first request, when an asset is missing or the template does not match the slots.
_ = app.Services.GetRequiredService<ChallengeTemplate>();
_ = app.Services.GetRequiredService<CertificateRenderer>();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseApiSecurityHeaders();
app.UseOriginCheck();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

var api = app.MapGroup("/api");
api.MapPublicEndpoints(devLogin);
api.MapAuthEndpoints(devLogin);
api.MapChallengeEndpoints();

app.Run();

// Lets the integration tests host the app.
public partial class Program;
