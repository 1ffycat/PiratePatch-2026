using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PiratePatch.Api.Auth;

/// <summary>The logged-in player, carried in the session cookie.</summary>
public readonly record struct PlayerIdentity(long TelegramId, string Handle);

public static class PlayerSession
{
    public static async Task SignInAsync(HttpContext http, TelegramUser user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.Username),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await http.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false });
    }

    public static Task SignOutAsync(HttpContext http) => http.SignOutAsync();

    /// <summary>The player of an authenticated request. Only call this behind <c>RequireAuthorization()</c>.</summary>
    public static PlayerIdentity GetPlayer(this ClaimsPrincipal principal) => new(
        long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture),
        principal.FindFirstValue(ClaimTypes.Name)!);

    public static PlayerIdentity? TryGetPlayer(this ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true ? principal.GetPlayer() : null;
}
