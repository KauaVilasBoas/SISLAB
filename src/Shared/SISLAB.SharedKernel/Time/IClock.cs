namespace SISLAB.SharedKernel.Time;

/// <summary>
/// Abstração de relógio — permite substituição em testes sem manipular DateTime.Now estático.
/// </summary>
public interface IClock
{
    /// <summary>Data e hora atual em UTC.</summary>
    DateTime UtcNow { get; }
}
