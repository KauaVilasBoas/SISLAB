using SISLAB.Api.Tests.Infrastructure;

namespace SISLAB.Api.Tests.Security;

/// <summary>
/// Verifies the baseline security response headers (card [E9] #58) are present on responses to any endpoint,
/// exercised end to end through the real HTTP pipeline via <see cref="SislabApiFactory"/>.
/// </summary>
public sealed class SecurityHeadersTests : IClassFixture<SislabApiFactory>
{
    private readonly SislabApiFactory _factory;

    public SecurityHeadersTests(SislabApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
    [InlineData("Permissions-Policy", "camera=(), microphone=(), geolocation=()")]
    [InlineData("X-XSS-Protection", "0")]
    public async Task Response_carries_the_security_header(string header, string expectedValue)
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.True(
            response.Headers.TryGetValues(header, out IEnumerable<string>? values),
            $"Expected security header '{header}' to be present.");
        Assert.Equal(expectedValue, Assert.Single(values!));
    }
}
