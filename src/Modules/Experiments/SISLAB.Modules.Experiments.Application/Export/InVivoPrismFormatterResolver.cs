using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Application.Export;

/// <summary>
/// Registry-backed <see cref="IInVivoPrismFormatterResolver"/>: indexes every <see cref="IInVivoPrismFormatter"/>
/// registered in DI by the <see cref="IInVivoPrismFormatter.FormulaCode"/> it declares and hands back the one
/// matching a requested code. A duplicate registration for the same code is a wiring error and fails fast at
/// construction (mirrors <see cref="PrismCsvFormatterResolver"/>).
/// </summary>
internal sealed class InVivoPrismFormatterResolver : IInVivoPrismFormatterResolver
{
    private readonly IReadOnlyDictionary<string, IInVivoPrismFormatter> _formatters;

    public InVivoPrismFormatterResolver(IEnumerable<IInVivoPrismFormatter> formatters)
        => _formatters = formatters.ToDictionary(
            formatter => formatter.FormulaCode,
            StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IInVivoPrismFormatter Resolve(string formulaCode)
        => _formatters.TryGetValue(formulaCode, out IInVivoPrismFormatter? formatter)
            ? formatter
            : throw new DomainException($"No in vivo Prism CSV formatter is registered for formula '{formulaCode}'.");
}
