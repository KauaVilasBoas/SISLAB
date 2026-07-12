using SISLAB.Modules.Inventory.Application.Partners;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.Partners;

public sealed class PartnerCommandHandlerTests
{
    [Fact]
    public async Task Register_creates_and_persists_the_partner()
    {
        var partners = new FakePartnerRepository();
        var handler = new RegisterPartnerCommandHandler(partners);

        Guid id = await handler.HandleAsync(new RegisterPartnerCommand(
            "Sigma-Aldrich", PartnerType.Supplier, Document: null,
            ContactEmail: "vendas.br@merck.com", Description: "Reagentes"));

        Partner created = Assert.IsType<Partner>(partners.LastAdded);
        Assert.Equal(id, created.Id);
        Assert.Equal("Sigma-Aldrich", created.Name);
        Assert.Equal("vendas.br@merck.com", created.ContactEmail!.Value);
    }

    [Fact]
    public async Task Update_changes_the_partner_and_persists_it()
    {
        Partner partner = Partner.Register("Old name", PartnerType.Client);
        var partners = new FakePartnerRepository().Seed(partner);
        var handler = new UpdatePartnerCommandHandler(partners);

        await handler.HandleAsync(new UpdatePartnerCommand(
            partner.Id, "Merck", PartnerType.Both, Document: "REG-1",
            ContactEmail: null, Description: "Insumos"));

        Assert.Equal("Merck", partner.Name);
        Assert.Equal(PartnerType.Both, partner.Type);
        Assert.Equal("REG-1", partner.Document);
        Assert.Same(partner, partners.LastUpdated);
    }

    [Fact]
    public async Task Update_fails_when_the_partner_does_not_exist()
    {
        var handler = new UpdatePartnerCommandHandler(new FakePartnerRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new UpdatePartnerCommand(
                Guid.NewGuid(), "X", PartnerType.Supplier, null, null, null)));
    }

    [Fact]
    public async Task Deactivate_takes_the_partner_out_of_service()
    {
        Partner partner = Partner.Register("Merck", PartnerType.Supplier);
        var partners = new FakePartnerRepository().Seed(partner);
        var handler = new DeactivatePartnerCommandHandler(partners);

        await handler.HandleAsync(new DeactivatePartnerCommand(partner.Id));

        Assert.False(partner.IsActive);
        Assert.Same(partner, partners.LastUpdated);
    }

    [Fact]
    public async Task Reactivate_puts_the_partner_back_in_service()
    {
        Partner partner = Partner.Register("Merck", PartnerType.Supplier);
        partner.Deactivate();
        var partners = new FakePartnerRepository().Seed(partner);
        var handler = new ReactivatePartnerCommandHandler(partners);

        await handler.HandleAsync(new ReactivatePartnerCommand(partner.Id));

        Assert.True(partner.IsActive);
        Assert.Same(partner, partners.LastUpdated);
    }

    [Fact]
    public async Task RecordSample_appends_a_sample_and_persists_it()
    {
        Partner partner = Partner.Register("Barbosa—UFBA", PartnerType.Client);
        var partners = new FakePartnerRepository().Seed(partner);
        var handler = new RecordPartnerSampleCommandHandler(partners);

        await handler.HandleAsync(new RecordPartnerSampleCommand(partner.Id, "GDA-43", "pendente"));

        SampleNote sample = Assert.Single(partner.Samples);
        Assert.Equal("GDA-43", sample.Reference);
        Assert.Same(partner, partners.LastUpdated);
    }

    [Fact]
    public async Task RemoveSample_drops_a_recorded_sample()
    {
        Partner partner = Partner.Register("Barbosa—UFBA", PartnerType.Client);
        partner.RecordSample(SampleNote.Create("GDA-43"));
        var partners = new FakePartnerRepository().Seed(partner);
        var handler = new RemovePartnerSampleCommandHandler(partners);

        await handler.HandleAsync(new RemovePartnerSampleCommand(partner.Id, "GDA-43"));

        Assert.Empty(partner.Samples);
        Assert.Same(partner, partners.LastUpdated);
    }
}
