namespace SISLAB.SharedKernel.Time;

/// <summary>
/// Implementação de produção de <see cref="IClock"/> delegando para o sistema operacional.
/// Registrar como Singleton no DI.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
