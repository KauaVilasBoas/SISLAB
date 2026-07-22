using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// A single "user is responsible for a step" link (card [E11] — step-scoped edit authority). It is a child of
/// the <see cref="ExperimentStep"/>, which is itself a child of the <see cref="Experiment"/> aggregate, so it is
/// only ever created/removed through the aggregate. The user is referenced <b>by value</b> via their Lumen user
/// id — never a cross-module FK or navigation (module isolation, section 2); membership of the user in the active
/// company is validated in the application layer through the Identity module's Contracts.
/// </summary>
/// <remarks>
/// Modelled as its own type (rather than a raw <see cref="Guid"/>) so it maps cleanly to the
/// <c>experiment_step_responsibles</c> junction table as an owned collection, with a surrogate key EF Core can
/// track. Equality is by the referenced <see cref="UserId"/>.
/// </remarks>
public sealed class ExperimentStepResponsible
{
    // Parameterless constructor for EF Core materialization.
    private ExperimentStepResponsible()
    {
    }

    private ExperimentStepResponsible(Guid id, Guid userId)
    {
        Id = id;
        UserId = userId;
    }

    /// <summary>Surrogate key for the junction row (EF Core tracking); not domain-meaningful.</summary>
    public Guid Id { get; private init; }

    /// <summary>The responsible user, referenced by value (Lumen user id).</summary>
    public Guid UserId { get; private init; }

    /// <summary>Creates a responsibility link for <paramref name="userId"/>.</summary>
    public static ExperimentStepResponsible For(Guid userId)
    {
        Guard.AgainstEmptyGuid(userId, nameof(userId));
        return new ExperimentStepResponsible(Guid.NewGuid(), userId);
    }
}
