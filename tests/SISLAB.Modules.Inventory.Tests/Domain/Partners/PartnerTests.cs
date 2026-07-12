using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.Modules.Inventory.Domain.Partners.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Tests.Domain.Partners;

public sealed class PartnerTests
{
    private static Partner NewSupplier() => Partner.Register("Sigma-Aldrich", PartnerType.Supplier);

    [Fact]
    public void Register_captures_the_attributes_normalizes_them_and_starts_active()
    {
        Partner partner = Partner.Register(
            name: "  Sigma-Aldrich  ",
            type: PartnerType.Both,
            document: "  12.345.678/0001-90  ",
            contactEmail: "  VENDAS.BR@Merck.com ",
            description: "  Reagentes, MTT, LPS, CFA  ");

        Assert.Equal("Sigma-Aldrich", partner.Name);
        Assert.Equal(PartnerType.Both, partner.Type);
        Assert.Equal("12.345.678/0001-90", partner.Document);
        Assert.Equal("vendas.br@merck.com", partner.ContactEmail!.Value);
        Assert.Equal("Reagentes, MTT, LPS, CFA", partner.Description);
        Assert.True(partner.IsActive);
        Assert.Empty(partner.Samples);
    }

    [Fact]
    public void Register_allows_a_partner_without_optional_contact_or_document()
    {
        Partner partner = Partner.Register("Fiocruz", PartnerType.Client);

        Assert.Null(partner.Document);
        Assert.Null(partner.ContactEmail);
        Assert.Null(partner.Description);
    }

    [Fact]
    public void Register_raises_PartnerRegistered()
    {
        Partner partner = NewSupplier();

        PartnerRegisteredEvent registered =
            Assert.IsType<PartnerRegisteredEvent>(Assert.Single(partner.DomainEvents));
        Assert.Equal(partner.Id, registered.PartnerId);
        Assert.Equal(PartnerType.Supplier, registered.Type);
    }

    [Fact]
    public void Register_rejects_a_blank_name()
        => Assert.Throws<DomainException>(() => Partner.Register("   ", PartnerType.Supplier));

    [Fact]
    public void Register_rejects_an_invalid_contact_email()
        => Assert.Throws<DomainException>(() =>
            Partner.Register("Merck", PartnerType.Supplier, contactEmail: "not-an-email"));

    [Fact]
    public void Partner_is_tenant_scoped()
        => Assert.IsAssignableFrom<ITenantEntity>(NewSupplier());

    [Theory]
    [InlineData(PartnerType.Supplier, true)]
    [InlineData(PartnerType.Both, true)]
    [InlineData(PartnerType.Client, false)]
    public void IsSupplier_reflects_the_type(PartnerType type, bool expected)
        => Assert.Equal(expected, Partner.Register("X", type).IsSupplier);

    [Fact]
    public void CanSupply_is_true_only_for_an_active_supplier()
    {
        Assert.True(NewSupplier().CanSupply());

        Partner client = Partner.Register("Barbosa—UFBA", PartnerType.Client);
        Assert.False(client.CanSupply());

        Partner inactive = NewSupplier();
        inactive.Deactivate();
        Assert.False(inactive.CanSupply());
    }

    [Fact]
    public void EnsureCanSupply_passes_for_an_active_supplier()
        => NewSupplier().EnsureCanSupply();

    [Fact]
    public void EnsureCanSupply_rejects_a_non_supplier()
        => Assert.Throws<BusinessException>(
            () => Partner.Register("Client", PartnerType.Client).EnsureCanSupply());

    [Fact]
    public void EnsureCanSupply_rejects_an_inactive_supplier()
    {
        Partner supplier = NewSupplier();
        supplier.Deactivate();

        Assert.Throws<BusinessException>(supplier.EnsureCanSupply);
    }

    [Fact]
    public void Rename_and_ChangeType_update_the_partner()
    {
        Partner partner = NewSupplier();

        partner.Rename("  Merck  ");
        partner.ChangeType(PartnerType.Both);

        Assert.Equal("Merck", partner.Name);
        Assert.Equal(PartnerType.Both, partner.Type);
    }

    [Fact]
    public void UpdateContactEmail_blank_clears_the_contact()
    {
        Partner partner = Partner.Register("Merck", PartnerType.Supplier, contactEmail: "a@b.com");

        partner.UpdateContactEmail("   ");

        Assert.Null(partner.ContactEmail);
    }

    [Fact]
    public void Deactivate_takes_the_partner_out_of_service_and_raises_an_event()
    {
        Partner partner = NewSupplier();
        partner.ClearDomainEvents();

        partner.Deactivate();

        Assert.False(partner.IsActive);
        Assert.IsType<PartnerDeactivatedEvent>(Assert.Single(partner.DomainEvents));
    }

    [Fact]
    public void Deactivate_is_idempotent()
    {
        Partner partner = NewSupplier();
        partner.Deactivate();
        partner.ClearDomainEvents();

        partner.Deactivate();

        Assert.Empty(partner.DomainEvents);
    }

    [Fact]
    public void Reactivate_puts_the_partner_back_in_service_and_raises_an_event()
    {
        Partner partner = NewSupplier();
        partner.Deactivate();
        partner.ClearDomainEvents();

        partner.Reactivate();

        Assert.True(partner.IsActive);
        Assert.IsType<PartnerReactivatedEvent>(Assert.Single(partner.DomainEvents));
    }

    [Fact]
    public void RecordSample_adds_a_sample()
    {
        Partner partner = NewSupplier();

        partner.RecordSample(SampleNote.Create("GDA-43", "pendente"));

        SampleNote sample = Assert.Single(partner.Samples);
        Assert.Equal("GDA-43", sample.Reference);
        Assert.Equal("pendente", sample.Status);
    }

    [Fact]
    public void RecordSample_rejects_a_duplicate_reference_case_insensitively()
    {
        Partner partner = NewSupplier();
        partner.RecordSample(SampleNote.Create("GDA-92", "pendente"));

        Assert.Throws<DomainException>(() => partner.RecordSample(SampleNote.Create("gda-92")));
    }

    [Fact]
    public void RemoveSample_removes_a_recorded_sample()
    {
        Partner partner = NewSupplier();
        partner.RecordSample(SampleNote.Create("GDA-92"));

        partner.RemoveSample("gda-92");

        Assert.Empty(partner.Samples);
    }

    [Fact]
    public void RemoveSample_rejects_an_unknown_reference()
    {
        Partner partner = NewSupplier();

        Assert.Throws<DomainException>(() => partner.RemoveSample("GDA-000"));
    }
}
