using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Experiments.Application.Experiments;
using SISLAB.Modules.Experiments.Application.Protocols;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Tests.Fakes;

/// <summary>Fixed actor accessor for handler tests — returns a stable identity without an HTTP principal.</summary>
internal sealed class FakeActorAccessor : IAuditActorAccessor
{
    private readonly string _actor;

    public FakeActorAccessor(string actor = "tester@lab") => _actor = actor;

    public string GetCurrentActor() => _actor;
}

/// <summary>
/// Fixed current-user context for handler tests — returns a stable Lumen user id without an HTTP principal, so
/// the responsibility-based authorization (card [E11]) can be exercised deterministically.
/// </summary>
internal sealed class FakeCurrentUserContext : ICurrentUserContext
{
    private readonly Guid _userId;

    public FakeCurrentUserContext(Guid userId) => _userId = userId;

    public Guid RequireUserId() => _userId;
}

/// <summary>
/// Fake of the Identity membership port: treats a fixed allow-list of user ids as active members of any company.
/// Defaults to allowing everyone when constructed without ids, so tests that do not care about the membership
/// guard stay terse.
/// </summary>
internal sealed class FakeCompanyMembershipQuery : ICompanyMembershipQuery
{
    private readonly HashSet<Guid>? _members;

    public FakeCompanyMembershipQuery(params Guid[] members)
        => _members = members.Length == 0 ? null : members.ToHashSet();

    public Task<bool> IsActiveMemberAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_members is null || _members.Contains(userId));
}

/// <summary>
/// Fake of the Configuration <see cref="ILabConfiguration"/> port (SISLAB-04). Treats a fixed allow-list of
/// experimental-model ids as existing for the active company; defaults to allowing every id when constructed empty,
/// so tests that do not care about the model-existence guard stay terse. Only the members the Experiments write-side
/// consumes are meaningful; the rest throw to make an unexpected dependency obvious.
/// </summary>
internal sealed class FakeLabConfiguration : ILabConfiguration
{
    private readonly HashSet<Guid>? _knownModels;

    public FakeLabConfiguration(params Guid[] knownModels)
        => _knownModels = knownModels.Length == 0 ? null : knownModels.ToHashSet();

    public Task<bool> ExperimentalModelExistsAsync(Guid modelId, CancellationToken ct)
        => Task.FromResult(_knownModels is null || _knownModels.Contains(modelId));

    public Task<ExperimentalModelDto?> GetExperimentalModelAsync(Guid modelId, CancellationToken ct)
        => throw new NotSupportedException("GetExperimentalModelAsync is not exercised by these tests.");

    public Task<int> GetExpiryWarningWindowDaysAsync(CancellationToken ct)
        => throw new NotSupportedException("GetExpiryWarningWindowDaysAsync is not exercised by these tests.");

    public Task<ItemCategoryDto?> GetItemCategoryAsync(Guid categoryId, CancellationToken ct)
        => throw new NotSupportedException("GetItemCategoryAsync is not exercised by these tests.");

    public Task<bool> ItemCategoryExistsAsync(Guid categoryId, CancellationToken ct)
        => throw new NotSupportedException("ItemCategoryExistsAsync is not exercised by these tests.");
}

/// <summary>An <see cref="ITenantContext"/> pinned to a fixed company, matching how the read side resolves it.</summary>
internal sealed class StubTenantContext : ITenantContext
{
    public StubTenantContext(Guid companyId) => CompanyId = companyId;

    public Guid CompanyId { get; }
}

/// <summary>Fixed clock for deterministic timestamps in tests.</summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}

/// <summary>
/// Real registry resolver wrapping the real strategies — so the calculate handler tests exercise the actual
/// Strategy resolution + formula, not a stub.
/// </summary>
internal static class TestProtocols
{
    public static IExperimentProtocolResolver Viability()
        => new ExperimentProtocolResolver(new IExperimentProtocol[] { new ViabilityCalculationStrategy() });

    /// <summary>Resolver holding every registered protocol, as the module wires it up.</summary>
    public static IExperimentProtocolResolver All()
        => new ExperimentProtocolResolver(new IExperimentProtocol[]
        {
            new ViabilityCalculationStrategy(),
            new NitricOxideCalculationStrategy(),
        });
}
