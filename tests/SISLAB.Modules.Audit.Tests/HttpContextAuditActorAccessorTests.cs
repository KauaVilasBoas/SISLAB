using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Audit.Infrastructure;

namespace SISLAB.Modules.Audit.Tests;

/// <summary>
/// Tests for <see cref="HttpContextAuditActorAccessor"/> (card [E9] #57): the audit actor is the JWT
/// <c>sub</c> claim (surfaced as <see cref="ClaimTypes.NameIdentifier"/>), falling back to
/// <c>"system"</c> when there is no HTTP principal (background work).
/// </summary>
public sealed class HttpContextAuditActorAccessorTests
{
    [Fact]
    public void Resolves_the_sub_claim_of_the_authenticated_user()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "auth0|abc")], authenticationType: "test"))
        };

        var accessor = new HttpContextAuditActorAccessor(StubHttpContext(context));

        Assert.Equal("auth0|abc", accessor.GetCurrentActor());
    }

    [Fact]
    public void Falls_back_to_system_when_there_is_no_http_context()
    {
        var accessor = new HttpContextAuditActorAccessor(StubHttpContext(httpContext: null));

        Assert.Equal(IAuditActorAccessor.SystemActor, accessor.GetCurrentActor());
    }

    [Fact]
    public void Falls_back_to_system_when_the_principal_has_no_sub_claim()
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

        var accessor = new HttpContextAuditActorAccessor(StubHttpContext(context));

        Assert.Equal(IAuditActorAccessor.SystemActor, accessor.GetCurrentActor());
    }

    private static IHttpContextAccessor StubHttpContext(HttpContext? httpContext) =>
        new HttpContextAccessor { HttpContext = httpContext };
}
