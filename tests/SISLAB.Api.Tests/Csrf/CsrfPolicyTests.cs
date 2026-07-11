using Microsoft.AspNetCore.Http;
using SISLAB.Api.Csrf;

namespace SISLAB.Api.Tests.Csrf;

/// <summary>
/// Tests for <see cref="CsrfPolicy"/>: the single source of truth for which requests
/// are exempt from CSRF validation (safe methods and public auth/infra paths).
/// </summary>
public sealed class CsrfPolicyTests
{
    [Theory]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    [InlineData("OPTIONS", true)]
    [InlineData("TRACE", true)]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("PATCH", false)]
    [InlineData("DELETE", false)]
    public void IsSafeMethod_ClassifiesHttpVerbs(string method, bool expectedSafe)
        => Assert.Equal(expectedSafe, CsrfPolicy.IsSafeMethod(method));

    [Theory]
    [InlineData("/api/auth/login", true)]
    [InlineData("/api/auth/csrf", true)]
    [InlineData("/health", true)]
    [InlineData("/swagger/index.html", true)]
    [InlineData("/api/inventory/items", false)]
    [InlineData("/api/companies/active", false)]
    public void IsExemptPath_MatchesPublicPrefixes(string path, bool expectedExempt)
        => Assert.Equal(expectedExempt, CsrfPolicy.IsExemptPath(new PathString(path)));

    [Fact]
    public void IsExemptPath_IsCaseInsensitive()
        => Assert.True(CsrfPolicy.IsExemptPath(new PathString("/API/Auth/Login")));
}
