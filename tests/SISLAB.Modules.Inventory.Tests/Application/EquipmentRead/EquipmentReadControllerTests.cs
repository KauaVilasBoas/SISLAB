using Microsoft.AspNetCore.Mvc;
using SISLAB.Modules.Inventory.Application.Equipments.Queries;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Tests.Application.EquipmentRead;

/// <summary>
/// Covers the read-side <see cref="EquipmentReadController"/> mapping (card [E4] #27): the detail endpoint keeps
/// the query pure (it returns <see cref="EquipmentDetail"/>?) and the controller is the single place that turns a
/// missing equipment into a <see cref="NotFoundException"/> (404), while a found one is wrapped in the uniform
/// <see cref="ApiResult{T}"/>. Exercised with a stub mediator, without a live database.
/// </summary>
public sealed class EquipmentReadControllerTests
{
    [Fact]
    public async Task Get_detail_throws_not_found_when_the_query_returns_null()
    {
        var controller = new EquipmentReadController(new StubMediator(detail: null));

        await Assert.ThrowsAsync<NotFoundException>(
            () => controller.GetEquipmentDetail(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Get_detail_wraps_the_found_equipment_in_the_api_result_envelope()
    {
        EquipmentDetail detail = SampleDetail();
        var controller = new EquipmentReadController(new StubMediator(detail));

        IActionResult result = await controller.GetEquipmentDetail(detail.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResult<EquipmentDetail>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Same(detail, payload.Data);
    }

    private static EquipmentDetail SampleDetail() => new(
        Id: Guid.NewGuid(),
        Name: "Plate reader",
        AssetTag: "PAT-0041",
        Status: EquipmentStatus.Available,
        StorageLocationId: null,
        StorageLocationName: null,
        NextCalibrationDate: null,
        CalibrationStatus: CalibrationStatus.NotRequired,
        IsActive: true,
        Manufacturer: "BioTek",
        Model: "Synergy H1",
        LastCalibrationDate: null,
        LastMaintenanceDate: null);

    /// <summary>Minimal <see cref="IMediator"/> that returns a pre-seeded detail (possibly null) for the detail query.</summary>
    private sealed class StubMediator : IMediator
    {
        private readonly EquipmentDetail? _detail;

        public StubMediator(EquipmentDetail? detail) => _detail = detail;

        public Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
            => request is GetEquipmentDetailQuery
                ? Task.FromResult((TResult)(object?)_detail!)
                : throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.");
    }
}
