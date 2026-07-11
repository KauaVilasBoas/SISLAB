using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SISLAB.Api.Csrf;
using SISLAB.Api.Middleware;
using SISLAB.SharedKernel.Http;

namespace SISLAB.Api.Tests.Csrf;

/// <summary>
/// Tests for <see cref="CsrfValidationMiddleware"/>: the double-submit-cookie CSRF gate
/// for the cookie-authenticated SPA flow (card #61).
///
/// Validation applies ONLY to unsafe methods, on non-exempt paths, when the request carries
/// the readable XSRF-TOKEN cookie (i.e. a browser session that armed CSRF protection).
/// </summary>
public sealed class CsrfValidationMiddlewareTests
{
    private const string XsrfCookie = "XSRF-TOKEN";

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SafeMethod_SkipsValidation_AndCallsNext(string method)
    {
        var antiforgery = new SpyAntiforgery(throwOnValidate: true);
        var (context, next) = BuildContext(method, "/api/inventory/items", withCsrfCookie: true);

        await InvokeAsync(context, antiforgery, next);

        Assert.True(next.Called);
        Assert.False(antiforgery.ValidateCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/refresh")]
    [InlineData("/health")]
    [InlineData("/swagger/index.html")]
    public async Task ExemptPath_SkipsValidation_AndCallsNext(string path)
    {
        var antiforgery = new SpyAntiforgery(throwOnValidate: true);
        var (context, next) = BuildContext("POST", path, withCsrfCookie: true);

        await InvokeAsync(context, antiforgery, next);

        Assert.True(next.Called);
        Assert.False(antiforgery.ValidateCalled);
    }

    [Fact]
    public async Task UnsafeMethod_WithoutCsrfCookie_SkipsValidation_PureBearerClient()
    {
        var antiforgery = new SpyAntiforgery(throwOnValidate: true);
        var (context, next) = BuildContext("POST", "/api/inventory/items", withCsrfCookie: false);

        await InvokeAsync(context, antiforgery, next);

        Assert.True(next.Called);
        Assert.False(antiforgery.ValidateCalled);
    }

    [Fact]
    public async Task UnsafeMethod_WithCookie_AndValidToken_CallsNext()
    {
        var antiforgery = new SpyAntiforgery(throwOnValidate: false);
        var (context, next) = BuildContext("POST", "/api/inventory/items", withCsrfCookie: true);

        await InvokeAsync(context, antiforgery, next);

        Assert.True(antiforgery.ValidateCalled);
        Assert.True(next.Called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnsafeMethod_WithCookie_AndInvalidToken_Returns403_WithApiResult()
    {
        var antiforgery = new SpyAntiforgery(throwOnValidate: true);
        var (context, next) = BuildContext("POST", "/api/inventory/items", withCsrfCookie: true);
        context.Response.Body = new MemoryStream();

        await InvokeAsync(context, antiforgery, next);

        Assert.True(antiforgery.ValidateCalled);
        Assert.False(next.Called); // request short-circuited
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        ApiResult? body = await ReadApiResultAsync(context);
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("CSRF token validation failed.", body.Message);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task OtherUnsafeMethods_WithCookie_AreValidated(string method)
    {
        var antiforgery = new SpyAntiforgery(throwOnValidate: false);
        var (context, next) = BuildContext(method, "/api/inventory/items", withCsrfCookie: true);

        await InvokeAsync(context, antiforgery, next);

        Assert.True(antiforgery.ValidateCalled);
        Assert.True(next.Called);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery, NextSpy next)
    {
        var middleware = new CsrfValidationMiddleware(
            next: next.Invoke,
            antiforgery: antiforgery,
            logger: NullLogger<CsrfValidationMiddleware>.Instance);

        await middleware.InvokeAsync(context);
    }

    private static (HttpContext Context, NextSpy Next) BuildContext(
        string method, string path, bool withCsrfCookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        if (withCsrfCookie)
            context.Request.Headers.Cookie = $"{XsrfCookie}=sample-token-value";

        return (context, new NextSpy());
    }

    private static async Task<ApiResult?> ReadApiResultAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        string json = await reader.ReadToEndAsync();

        return JsonSerializer.Deserialize<ApiResult>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private sealed class NextSpy
    {
        public bool Called { get; private set; }

        public Task Invoke(HttpContext _)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Test double for <see cref="IAntiforgery"/>: records whether validation ran and either
    /// throws (simulating a forged/missing token) or completes (valid token).
    /// </summary>
    private sealed class SpyAntiforgery : IAntiforgery
    {
        private readonly bool _throwOnValidate;

        public SpyAntiforgery(bool throwOnValidate) => _throwOnValidate = throwOnValidate;

        public bool ValidateCalled { get; private set; }

        public Task ValidateRequestAsync(HttpContext context)
        {
            ValidateCalled = true;
            if (_throwOnValidate)
                throw new AntiforgeryValidationException("Invalid CSRF token.");

            return Task.CompletedTask;
        }

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext context)
            => new("request", "cookie", "X-XSRF-TOKEN", "XSRF-TOKEN");

        public AntiforgeryTokenSet GetTokens(HttpContext context)
            => new("request", "cookie", "X-XSRF-TOKEN", "XSRF-TOKEN");

        public Task<bool> IsRequestValidAsync(HttpContext context)
            => Task.FromResult(!_throwOnValidate);

        public void SetCookieTokenAndHeader(HttpContext context) { }
    }
}
