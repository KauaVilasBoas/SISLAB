namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>Biological sex of an <see cref="Animal"/> enrolled in an in vivo study (card [E11] #73).</summary>
public enum AnimalSex
{
    /// <summary>Sex not recorded / not applicable.</summary>
    Unspecified = 0,

    /// <summary>Male.</summary>
    Male = 1,

    /// <summary>Female.</summary>
    Female = 2,
}
