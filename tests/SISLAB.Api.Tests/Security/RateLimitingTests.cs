using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using SISLAB.Api.Tests.Infrastructure;

namespace SISLAB.Api.Tests.Security;

/// <summary>
/// Verifies the per-IP rate limiting (card [E9] #58): the strict "login" window on <c>/api/auth/*</c>
/// returns <c>429 Too Many Requests</c> once the ceiling is exceeded, exercised end to end through the real
/// HTTP pipeline via <see cref="SislabApiFactory"/>.
/// </summary>
public sealed class RateLimitingTests : IClassFixture<SislabApiFactory>
{
    // Must match RateLimitingConfiguration's "login" ceiling (10 requests/minute).
    private const int LoginPermitPerMinute = 10;

    private readonly SislabApiFactory _factory;

    public RateLimitingTests(SislabApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Auth_endpoint_returns_429_after_exceeding_the_login_window()
    {
        // The CSRF token endpoint lives under /api/auth, so it falls in the strict "login" partition.
        HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Spend the whole window (all requests share one IP partition on the test server).
        for (int i = 0; i < LoginPermitPerMinute; i++)
        {
            HttpResponseMessage allowed = await client.GetAsync("/api/auth/csrf");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        HttpResponseMessage rejected = await client.GetAsync("/api/auth/csrf");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        string body = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("Too many requests.", body);
    }

    [Fact]
    public async Task Health_endpoint_stays_within_the_generous_api_window()
    {
        HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // A handful of health probes is far below the 300/min "api" ceiling, so none is throttled.
        for (int i = 0; i < 15; i++)
        {
            HttpResponseMessage response = await client.GetAsync("/health");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }
}
