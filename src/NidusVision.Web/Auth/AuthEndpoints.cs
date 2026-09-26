using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NidusVision.Core.Models;

namespace NidusVision.Web.Auth;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").AllowAnonymous();
        group.MapGet("/status", GetStatus);
        group.MapPost("/setup", Setup);
        group.MapPost("/login", Login);
        group.MapPost("/logout", (Delegate)Logout);
    }

    private static async Task<IResult> GetStatus(LocalAuthService auth, HttpContext http, CancellationToken cancellationToken)
    {
        var configured = await auth.IsConfiguredAsync(cancellationToken);
        return TypedResults.Ok(new AuthStatusResponse(configured, http.User.Identity?.IsAuthenticated is true));
    }

    private static async Task<IResult> Setup(LocalAuthService auth, [FromBody] PasswordRequest request, HttpContext http, CancellationToken cancellationToken)
    {
        try
        {
            await auth.SetupAsync(request.Password, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(new { message = ex.Message });
        }

        await SignInAsync(http);
        return TypedResults.Ok(new AuthStatusResponse(true, true));
    }

    private static async Task<IResult> Login(LocalAuthService auth, LoginThrottle throttle, [FromBody] PasswordRequest request, HttpContext http, CancellationToken cancellationToken)
    {
        var client = http.Connection.RemoteIpAddress;
        if (throttle.IsLockedOut(client, out var retryAfter))
        {
            http.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return TypedResults.Json(
                new { message = "Too many failed sign-in attempts. Try again shortly." },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (!await auth.IsConfiguredAsync(cancellationToken))
        {
            return TypedResults.BadRequest(new { message = "Set a password first." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || !await auth.VerifyAsync(request.Password, cancellationToken))
        {
            throttle.RecordFailure(client);
            return TypedResults.Unauthorized();
        }

        throttle.RecordSuccess(client);
        await SignInAsync(http);
        return TypedResults.Ok(new AuthStatusResponse(true, true));
    }

    private static async Task<IResult> Logout(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.Ok();
    }

    private static Task SignInAsync(HttpContext http)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "admin")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}

internal static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddNidusAuth(this IServiceCollection services)
    {
        services.AddSingleton<PasswordHasher<LocalUser>>();
        services.AddScoped<LocalAuthService>();
        services.AddSingleton<LoginThrottle>();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "nidus.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = true;
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
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        return services;
    }
}
