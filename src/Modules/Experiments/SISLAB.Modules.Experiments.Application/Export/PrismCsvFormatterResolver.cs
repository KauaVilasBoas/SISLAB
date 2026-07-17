using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Application.Export;

/// <summary>
/// Registry-backed <see cref="IPrismCsvFormatterResolver"/>: indexes every <see cref="IPrismCsvFormatter"/>
/// registered in DI by the <see cref="IPrismCsvFormatter.FormulaCode"/> it declares and hands back the one
/// matching a requested code. A duplicate registration for the same code is a wiring error and fails fast at
/// construction.
/// </summary>
internal sealed class PrismCsvFormatterResolver : IPrismCsvFormatterResolver
{
    private readonly IReadOnlyDictionary<string, IPrismCsvFormatter> _formatters;

    public PrismCsvFormatterResolver(IEnumerable<IPrismCsvFormatter> formatters)
        => _formatters = formatters.ToDictionary(
            formatter => formatter.FormulaCode,
            StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IPrismCsvFormatter Resolve(string formulaCode)
        => _formatters.TryGetValue(formulaCode, out IPrismCsvFormatter? formatter)
            ? formatter
            : throw new DomainException($"No Prism CSV formatter is registered for formula '{formulaCode}'.");
}
