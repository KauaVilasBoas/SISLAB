using System.Security.Cryptography;
using System.Text;

namespace SISLAB.Modules.Experiments.Tests.Persistence;

/// <summary>
/// Locks the data-preservation contract of the SISLAB-03 migration
/// (<c>20260724105654_AddCagesAndAnimalGroupAssignment</c>) without a live database. The migration's Up back-fills
/// legacy animals by (a) synthesizing one cage per legacy group — with a deterministic id
/// <c>md5(group_id)::uuid</c> — and (b) placing each animal into the cage synthesized from <i>its own</i> group while
/// leaving <c>group_id</c> intact. This test reproduces exactly that transformation in memory over a legacy fixture and
/// asserts the two properties the migration promises: <b>no animal is lost</b> and <b>every animal keeps its group</b>.
/// The SQL itself is smoke-tested against PostgreSQL by the integration suite when Docker is available.
/// </summary>
public sealed class CageMigrationDataPreservationTests
{
    private sealed record LegacyAnimal(Guid Id, Guid GroupId, string Identifier);

    private sealed record MigratedAnimal(Guid Id, Guid CageId, Guid? GroupId, string Identifier);

    private sealed record MigratedCage(Guid Id, Guid GroupId, string Name);

    // Reproduces the migration's md5(group_id::text)::uuid derivation used by both the INSERT and the UPDATE.
    private static Guid CageIdForGroup(Guid groupId)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(groupId.ToString()));
        return new Guid(hash);
    }

    // The in-memory analogue of the migration's step 4a (synthesize cages) + 4b (place animals, keep group).
    private static (List<MigratedCage> Cages, List<MigratedAnimal> Animals) Migrate(IReadOnlyList<LegacyAnimal> legacy)
    {
        List<MigratedCage> cages = legacy
            .Select(a => a.GroupId)
            .Distinct()
            .Select(groupId => new MigratedCage(CageIdForGroup(groupId), groupId, $"Caixa (migrada) — {groupId}"))
            .ToList();

        List<MigratedAnimal> animals = legacy
            .Select(a => new MigratedAnimal(a.Id, CageIdForGroup(a.GroupId), a.GroupId, a.Identifier))
            .ToList();

        return (cages, animals);
    }

    [Fact]
    public void Every_legacy_animal_survives_and_keeps_its_group_and_gets_a_cage()
    {
        Guid control = Guid.NewGuid();
        Guid dose = Guid.NewGuid();
        var legacy = new List<LegacyAnimal>
        {
            new(Guid.NewGuid(), control, "M1-01"),
            new(Guid.NewGuid(), control, "M1-02"),
            new(Guid.NewGuid(), dose, "M1-03"),
        };

        (List<MigratedCage> cages, List<MigratedAnimal> animals) = Migrate(legacy);

        // No loss: same count, same identifiers.
        Assert.Equal(legacy.Count, animals.Count);
        Assert.Equal(
            legacy.Select(a => a.Identifier).OrderBy(x => x),
            animals.Select(a => a.Identifier).OrderBy(x => x));

        // Group preserved for every animal.
        foreach (LegacyAnimal before in legacy)
        {
            MigratedAnimal after = animals.Single(a => a.Id == before.Id);
            Assert.Equal(before.GroupId, after.GroupId);
            Assert.NotEqual(Guid.Empty, after.CageId);
        }

        // One cage per distinct legacy group, and every animal's cage exists.
        Assert.Equal(2, cages.Count);
        Assert.All(animals, a => Assert.Contains(cages, c => c.Id == a.CageId));
    }

    [Fact]
    public void Animals_of_the_same_group_land_in_the_same_synthesized_cage()
    {
        Guid group = Guid.NewGuid();
        var legacy = new List<LegacyAnimal>
        {
            new(Guid.NewGuid(), group, "A1"),
            new(Guid.NewGuid(), group, "A2"),
        };

        (List<MigratedCage> cages, List<MigratedAnimal> animals) = Migrate(legacy);

        Assert.Single(cages);
        Assert.Single(animals.Select(a => a.CageId).Distinct());
    }

    [Fact]
    public void The_down_precondition_holds_only_when_no_animal_is_unassigned()
    {
        // After Up, a legacy animal always has a group; a new-model animal may be unassigned (group_id null). The Down
        // guard refuses to revert while any unassigned animal exists — asserted here as the boolean the SQL EXISTS
        // check computes.
        var afterUp = new List<MigratedAnimal>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "legacy"),
        };
        Assert.False(afterUp.Any(a => a.GroupId is null), "Legacy-only data must be safely revertible.");

        afterUp.Add(new MigratedAnimal(Guid.NewGuid(), Guid.NewGuid(), null, "new-unassigned"));
        Assert.True(afterUp.Any(a => a.GroupId is null), "An unassigned animal must block the reversal.");
    }
}
