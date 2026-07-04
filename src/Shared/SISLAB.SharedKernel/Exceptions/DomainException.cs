namespace SISLAB.SharedKernel.Exceptions;

/// <summary>
/// Exceção base para violações de regras de negócio (invariantes de domínio).
/// Resulta em HTTP 422 Unprocessable Entity ou 400, dependendo da política da API.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}
