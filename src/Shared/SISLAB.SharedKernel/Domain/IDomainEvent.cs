namespace SISLAB.SharedKernel.Domain;

/// <summary>
/// Marcador de evento de domínio. Eventos de domínio são internos ao bounded context
/// e descrevem algo que aconteceu no domínio (passado, imutável).
/// </summary>
public interface IDomainEvent
{
    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    DateTime OccurredOnUtc { get; }
}
