using SISLAB.Modules.Configuration.Domain.CollectionRoles;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Tests.Domain.CollectionRoles;

/// <summary>
/// Covers the collection role aggregate (SISLAB-08): a per-tenant catalogue entry whose name/description are always
/// inputs (nothing lab-specific is a code constant), trimmed and length-guarded.
/// </summary>
public sealed class CollectionRoleTests
{
    [Fact]
    public void Create_builds_a_role_from_inputs()
    {
        CollectionRole role = CollectionRole.Create("Anestesia", "Aplica o anestésico antes da coleta.");

        Assert.Equal("Anestesia", role.Name);
        Assert.Equal("Aplica o anestésico antes da coleta.", role.Description);
    }

    [Fact]
    public void Create_trims_the_name_and_normalizes_a_blank_description_to_null()
    {
        CollectionRole role = CollectionRole.Create("  Volante  ", "   ");

        Assert.Equal("Volante", role.Name);
        Assert.Null(role.Description);
    }

    [Fact]
    public void Create_rejects_a_blank_name()
        => Assert.Throws<DomainException>(() => CollectionRole.Create("   "));

    [Fact]
    public void Rename_replaces_the_name_keeping_identity()
    {
        CollectionRole role = CollectionRole.Create("Sangue");
        Guid id = role.Id;

        role.Rename("Sangue periférico");

        Assert.Equal(id, role.Id);
        Assert.Equal("Sangue periférico", role.Name);
    }

    [Fact]
    public void ChangeDescription_can_set_and_clear()
    {
        CollectionRole role = CollectionRole.Create("Medula");

        role.ChangeDescription("Coleta de medula óssea.");
        Assert.Equal("Coleta de medula óssea.", role.Description);

        role.ChangeDescription(null);
        Assert.Null(role.Description);
    }
}
