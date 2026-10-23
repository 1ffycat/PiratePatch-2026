using Microsoft.Extensions.Options;
using PiratePatch.Api.Configuration;

namespace PiratePatch.Api.Hosting;

public static class SecurityMiddleware
{
    /// <summary>Defence in depth for the API's own responses; nginx sets the full set, including CSP, for the site.</summary>
    public static IApplicationBuilder UseApiSecurityHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        context.Response.OnStarting(static state =>
        {
            var headers = ((HttpContext)state).Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            return Task.CompletedTask;
        }, context);

        return next(context);
    });

    /// <summary>
    /// CSRF protection for the cookie session: a state-changing request must come from the site's own origin.
    /// Browsers always send <c>Origin</c> on such requests. The site is single-origin, so there is no CORS at all.
    /// </summary>
    public static IApplicationBuilder UseOriginCheck(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        var request = context.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
            return next(context);

        var site = context.RequestServices.GetRequiredService<IOptions<SiteOptions>>().Value.PublicUrl;
        var allowed = request.Headers.Origin.Count == 1
            && Uri.TryCreate(request.Headers.Origin[0], UriKind.Absolute, out var origin)
            && origin.GetLeftPart(UriPartial.Authority) == site.GetLeftPart(UriPartial.Authority);

        if (allowed)
            return next(context);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Results.Problem("Wrong origin.", statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context);
    });
}
