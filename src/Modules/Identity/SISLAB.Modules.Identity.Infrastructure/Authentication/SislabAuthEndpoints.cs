using System.Security.Claims;
using Lumen.Identity.Application.Auth.ChangePassword;
using Lumen.Identity.Application.Auth.ConfirmEmail;
using Lumen.Identity.Application.Auth.ForgotPassword;
using Lumen.Identity.Application.Auth.Login;
using Lumen.Identity.Application.Auth.Logout;
using Lumen.Identity.Application.Auth.Refresh;
using Lumen.Identity.Application.Auth.Register;
using Lumen.Identity.Application.Auth.ResendConfirmationEmail;
using Lumen.Identity.Application.Auth.ResetPassword;
using Lumen.Identity.Application.Users.GetCurrentUser;
using Lumen.Identity.Infrastructure.Configuration;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace SISLAB.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// SISLAB authentication surface — the backend bridge that turns Lumen's body-only token flow into
/// the <b>httpOnly cookie</b> session the SPA requires (card [E7] #44, decision fixed by the product
/// owner: cookies, never localStorage/Bearer in the browser).
///
/// <para>This deliberately <b>replaces</b> <c>MapLumenIdentityEndpoints</c>. Both cannot map the same
/// routes (ASP.NET raises <c>AmbiguousMatchException</c>), and Lumen exposes no per-endpoint mapping to
/// override just login/refresh/logout. So SISLAB owns the whole <c>/api/auth</c> + <c>/api/me</c> surface
/// and dispatches the very same Lumen CQRS commands via MediatR — Lumen stays confined to Infrastructure
/// (§8), and every endpoint keeps its current path. The only behavioural change over Lumen's own mapping is
/// the cookie handling on <c>login</c>/<c>refresh</c>/<c>logout</c>.</para>
///
/// <list type="bullet">
///   <item><c>POST /api/auth/login</c> → writes the access + refresh httpOnly cookies, returns the body too.</item>
///   <item><c>POST /api/auth/refresh</c> → reads the refresh cookie (falls back to body), rotates both cookies.</item>
///   <item><c>POST /api/auth/logout</c> → revokes the refresh token and clears both cookies.</item>
///   <item>register / confirm-email / resend-confirmation / forgot-password / reset-password /
///     me / change-password → thin pass-throughs preserving Lumen's contract and paths.</item>
/// </list>
/// </summary>
public static class SislabAuthEndpoints
{
    private const string AuthPrefix = "/api/auth";
    private const string MePrefix = "/api/me";

    public static IEndpointRouteBuilder MapSislabAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder auth = endpoints.MapGroup(AuthPrefix).WithTags("Identity");

        // ---- Cookie-bridged endpoints ------------------------------------------------------------

        auth.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .Produces<LoginResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);

        auth.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .Produces<RefreshTokenResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // ---- Pass-through endpoints (preserve Lumen's contract & paths) ---------------------------

        auth.MapPost("/register", async (RegisterCommand command, IMediator mediator, CancellationToken ct) =>
        {
            RegisterResult result = await mediator.Send(command, ct);
            return Results.Created($"/api/users/{result.Id}", result);
        })
        .AllowAnonymous()
        .Produces<RegisterResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapGet("/confirm-email", async (string token, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new ConfirmEmailCommand(token), ct);
            return Results.Ok();
        })
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        auth.MapPost("/resend-confirmation", async (EmailRequest req, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new ResendConfirmationEmailCommand(req.Email), ct);
            return Results.Ok();
        })
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        auth.MapPost("/forgot-password", async (EmailRequest req, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new ForgotPasswordCommand(req.Email), ct);
            return Results.Ok();
        })
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        auth.MapPost("/reset-password", async (ResetPasswordRequest req, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new ResetPasswordCommand(req.Token, req.NewPassword), ct);
            return Results.NoContent();
        })
        .AllowAnonymous()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // ---- /api/me surface ---------------------------------------------------------------------

        RouteGroupBuilder me = endpoints.MapGroup(MePrefix).WithTags("Identity").RequireAuthorization();

        me.MapGet("/", async (HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        {
            Guid userId = GetUserId(ctx);
            if (userId == Guid.Empty) return Results.Unauthorized();

            GetCurrentUserResult? result = await mediator.Send(new GetCurrentUserQuery(userId), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .Produces<GetCurrentUserResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        me.MapPost("/change-password", async (ChangePasswordRequest req, HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        {
            Guid userId = GetUserId(ctx);
            if (userId == Guid.Empty) return Results.Unauthorized();

            await mediator.Send(new ChangePasswordCommand(userId, req.CurrentPassword, req.NewPassword), ct);
            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest req,
        HttpContext ctx,
        IMediator mediator,
        IOptions<IdentityJwtOptions> jwtOptions,
        CancellationToken ct)
    {
        string ip = ClientIp(ctx);
        LoginResult result = await mediator.Send(new LoginCommand(req.Identifier, req.Password, ip), ct);

        WriteSessionCookies(ctx, result.AccessToken, result.RefreshToken, result.ExpiresIn, jwtOptions.Value);
        return Results.Ok(result);
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext ctx,
        IMediator mediator,
        IOptions<IdentityJwtOptions> jwtOptions,
        RefreshRequest? req,
        CancellationToken ct)
    {
        // The browser sends the refresh token via the httpOnly cookie; non-browser callers may still
        // pass it in the body. Cookie wins when present.
        string? refreshToken = ctx.Request.Cookies[SessionCookies.RefreshTokenName];
        if (string.IsNullOrWhiteSpace(refreshToken))
            refreshToken = req?.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
            return Results.Unauthorized();

        string ip = ClientIp(ctx);
        RefreshTokenResult result = await mediator.Send(new RefreshTokenCommand(refreshToken, ip), ct);

        WriteSessionCookies(ctx, result.AccessToken, result.RefreshToken, result.ExpiresIn, jwtOptions.Value);
        return Results.Ok(result);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext ctx,
        IMediator mediator,
        LogoutRequest? req,
        CancellationToken ct)
    {
        Guid userId = GetUserId(ctx);
        if (userId == Guid.Empty) return Results.Unauthorized();

        // Prefer the cookie so the SPA never has to read/echo the refresh token.
        string? refreshToken = ctx.Request.Cookies[SessionCookies.RefreshTokenName] ?? req?.RefreshToken;

        await mediator.Send(new LogoutCommand(refreshToken, userId, ClientIp(ctx)), ct);

        // Always clear the browser session, even if the token was already gone/revoked server-side.
        SessionCookies.Clear(ctx.Response, isSecure: ctx.Request.IsHttps);
        return Results.NoContent();
    }

    private static void WriteSessionCookies(
        HttpContext ctx, string accessToken, string refreshToken, int expiresInSeconds, IdentityJwtOptions jwt)
    {
        SessionCookies.Write(
            ctx.Response,
            accessToken,
            refreshToken,
            isSecure: ctx.Request.IsHttps,
            accessMaxAge: TimeSpan.FromSeconds(expiresInSeconds),
            refreshMaxAge: TimeSpan.FromDays(jwt.RefreshExpirationDays));
    }

    private static string ClientIp(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static Guid GetUserId(HttpContext ctx) =>
        Guid.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid id) ? id : Guid.Empty;

    // Request bodies — mirror Lumen's endpoint contracts so the SPA/API surface is unchanged.
    private sealed record LoginRequest(string Identifier, string Password);
    private sealed record RefreshRequest(string? RefreshToken);
    private sealed record LogoutRequest(string? RefreshToken);
    private sealed record EmailRequest(string Email);
    private sealed record ResetPasswordRequest(string Token, string NewPassword);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
