using Microsoft.AspNetCore.Mvc;
using SISLAB.Modules.Inventory.Application.PartnerRead;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Tests.Application.PartnerRead;

/// <summary>
/// Covers the read-side <see cref="PartnerReadController"/> mapping (card [E4] #28): the detail endpoint keeps the
/// query pure (it returns <see cref="PartnerDetail"/>?) and the controller is the single place that turns a missing
/// partner into a <see cref="NotFoundException"/> (404), while a found one is wrapped in the uniform
/// <see cref="ApiResult{T}"/>. Exercised with a stub mediator, without a live database.
/// </summary>
public sealed class PartnerReadControllerTests
{
    [Fact]
    public async Task Get_detail_throws_not_found_when_the_query_returns_null()
    {
        var controller = new PartnerReadController(new StubMediator(detail: null));

        await Assert.ThrowsAsync<NotFoundException>(
            () => controller.GetPartnerDetail(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Get_detail_wraps_the_found_partner_in_the_api_result_envelope()
    {
        PartnerDetail detail = SampleDetail();
        var controller = new PartnerReadController(new StubMediator(detail));

        IActionResult result = await controller.GetPartnerDetail(detail.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResult<PartnerDetail>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Same(detail, payload.Data);
    }

    private static PartnerDetail SampleDetail() => new(
        Id: Guid.NewGuid(),
        Name: "Sigma-Aldrich (Merck)",
        Type: PartnerType.Supplier,
        Cnpj: "12.345.678/0001-90",
        IsActive: true,
        Email: "vendas.br@merck.com",
        Notes: "Reagentes, MTT, LPS, CFA");

    /// <summary>Minimal <see cref="IMediator"/> that returns a pre-seeded detail (possibly null) for the detail query.</summary>
    private sealed class StubMediator : IMediator
    {
        private readonly PartnerDetail? _detail;

        public StubMediator(PartnerDetail? detail) => _detail = detail;

        public Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
            => request is GetPartnerDetailQuery
                ? Task.FromResult((TResult)(object?)_detail!)
                : throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.");
    }
}
