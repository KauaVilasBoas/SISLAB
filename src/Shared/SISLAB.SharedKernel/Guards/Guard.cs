using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.SharedKernel.Guards;

/// <summary>
/// Guard clauses para validação de pré-condições em domínio e application layer.
/// Cada método lança <see cref="DomainException"/> quando a condição não é satisfeita,
/// evitando que estados inválidos entrem nos agregados.
/// </summary>
public static class Guard
{
    /// <summary>Garante que o valor não seja nulo.</summary>
    public static T AgainstNull<T>(T? value, string parameterName)
        where T : class
    {
        if (value is null)
            throw new DomainException($"'{parameterName}' não pode ser nulo.");

        return value;
    }

    /// <summary>Garante que a string não seja nula, vazia ou apenas espaços.</summary>
    public static string AgainstNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"'{parameterName}' não pode ser vazio ou nulo.");

        return value;
    }

    /// <summary>Garante que o valor numérico seja maior que zero.</summary>
    public static decimal AgainstNonPositive(decimal value, string parameterName)
    {
        if (value <= 0)
            throw new DomainException($"'{parameterName}' deve ser maior que zero. Valor recebido: {value}.");

        return value;
    }

    /// <summary>Garante que o valor numérico seja maior ou igual a zero.</summary>
    public static decimal AgainstNegative(decimal value, string parameterName)
    {
        if (value < 0)
            throw new DomainException($"'{parameterName}' não pode ser negativo. Valor recebido: {value}.");

        return value;
    }

    /// <summary>Garante que o Guid não seja o valor padrão (Guid.Empty).</summary>
    public static Guid AgainstEmptyGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new DomainException($"'{parameterName}' não pode ser um Guid vazio.");

        return value;
    }

    /// <summary>Garante que a string não exceda um comprimento máximo.</summary>
    public static string AgainstMaxLength(string value, int maxLength, string parameterName)
    {
        if (value.Length > maxLength)
            throw new DomainException($"'{parameterName}' excede o comprimento máximo de {maxLength} caracteres.");

        return value;
    }
}
