using SISLAB.Modules.Identity.Infrastructure.Multitenancy;

namespace SISLAB.Modules.Identity.Tests.Multitenancy;

/// <summary>
/// Testes do <see cref="TenantContext"/> — implementação concreta e mutável de ITenantContext.
/// </summary>
public sealed class TenantContextTests
{
    [Fact]
    public void NovoContexto_CompanyIdVazio()
    {
        var ctx = new TenantContext();

        Assert.Equal(Guid.Empty, ctx.CompanyId);
        Assert.False(ctx.HasCompany);
    }

    [Fact]
    public void SetCompany_ComGuidValido_DefineCompany()
    {
        var ctx = new TenantContext();
        var companyId = Guid.NewGuid();

        ctx.SetCompany(companyId);

        Assert.Equal(companyId, ctx.CompanyId);
        Assert.True(ctx.HasCompany);
    }

    [Fact]
    public void SetCompany_ComGuidVazio_Lanca()
        => Assert.Throws<ArgumentException>(() => new TenantContext().SetCompany(Guid.Empty));
}
