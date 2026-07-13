using SISLAB.Modules.Configuration.Domain;
using SISLAB.Modules.Configuration.Domain.ItemCategories;

namespace SISLAB.Modules.Configuration.Tests.Domain;

/// <summary>
/// Pins the load-bearing contract of <see cref="DeterministicGuid"/> (card [E12] #76): the id it derives must
/// equal <c>md5(namespace || value)::uuid</c> in PostgreSQL — that byte-for-byte agreement is what lets the C#
/// tenant seeder and the SQL enum→category backfill produce the same category ids without a lookup table. If
/// this test breaks, the seed and the migration would disagree and the backfill would orphan every item.
/// </summary>
public sealed class DeterministicGuidTests
{
    [Fact]
    public void From_matches_the_canonical_postgres_md5_uuid()
    {
        // Expected value is md5('sislab:item-category:11111111-...-111111111111' || 'Solvent') laid out as a
        // UUID in canonical (big-endian) order — exactly what PostgreSQL md5(...)::uuid yields for the same
        // input. Computed independently (see the md5 of that concatenation), so this asserts the SQL agreement,
        // not just internal consistency.
        var company = Guid.Parse("11111111-1111-1111-1111-111111111111");

        Guid id = DeterministicGuid.From(
            DefaultItemCategories.IdNamespace + DeterministicGuid.CompanyToken(company),
            "Solvent");

        Assert.Equal(Guid.Parse("b4a8d66d-41c5-9f01-6c7a-6c014a16cd21"), id);
    }

    [Fact]
    public void From_is_deterministic_for_the_same_input()
    {
        Assert.Equal(
            DeterministicGuid.From("ns:", "value"),
            DeterministicGuid.From("ns:", "value"));
    }

    [Fact]
    public void From_differs_when_the_namespace_or_value_differs()
    {
        Assert.NotEqual(
            DeterministicGuid.From("ns-a:", "value"),
            DeterministicGuid.From("ns-b:", "value"));

        Assert.NotEqual(
            DeterministicGuid.From("ns:", "value-a"),
            DeterministicGuid.From("ns:", "value-b"));
    }

    [Fact]
    public void CompanyToken_is_the_lowercase_canonical_uuid_text()
    {
        var company = Guid.Parse("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE");

        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", DeterministicGuid.CompanyToken(company));
    }

    [Fact]
    public void The_category_deterministic_id_uses_the_postgres_agreeing_derivation()
    {
        var company = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // DefaultItemCategories.DeterministicId must route through DeterministicGuid (the SQL-agreeing scheme),
        // since categories are the catalogue the SQL backfill recomputes — so it lands on the same pinned id.
        Assert.Equal(
            Guid.Parse("b4a8d66d-41c5-9f01-6c7a-6c014a16cd21"),
            DefaultItemCategories.DeterministicId(company, "Solvent"));
    }
}
