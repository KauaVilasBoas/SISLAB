namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Tipo unitário (void equivalente para generics).
/// Usado como TResult em commands que não retornam valor de negócio.
/// </summary>
public readonly struct Unit
{
    /// <summary>Valor singleton de Unit.</summary>
    public static readonly Unit Value = default;
}
