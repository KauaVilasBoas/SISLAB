using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

/// <summary>
/// Validates the reporting window (<c>[From, To]</c>) shared by the consumption read-side queries
/// (<see cref="GetConsumptionReportQuery"/> and <see cref="GetConsumptionSeriesQuery"/>, card [E4] #31). The
/// window is a required input: an unset or inverted range is a caller error, not a domain invariant, so it is
/// surfaced as a <see cref="BusinessException"/> (422) rather than allowed to reach the database.
/// </summary>
internal static class ConsumptionWindow
{
    /// <summary>
    /// Ensures <paramref name="from"/> and <paramref name="to"/> form a usable range: both supplied (a
    /// default <see cref="DateOnly"/> means the caller forgot the parameter) and not inverted
    /// (<paramref name="to"/> on or after <paramref name="from"/>). Throws <see cref="BusinessException"/>
    /// (mapped to 422) otherwise; a single-day window (<c>from == to</c>) is valid.
    /// </summary>
    public static void EnsureValid(DateOnly from, DateOnly to)
    {
        if (from == default || to == default)
            throw new BusinessException("A reporting window ('from' and 'to') is required.");

        if (to < from)
            throw new BusinessException("The reporting window end ('to') cannot be before its start ('from').");
    }
}
