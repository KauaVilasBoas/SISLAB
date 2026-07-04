namespace SISLAB.SharedKernel.Exceptions;

/// <summary>
/// Acesso negado: o usuário está autenticado mas não tem permissão para a operação.
/// HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }

    public ForbiddenException()
        : base("Você não tem permissão para realizar esta operação.") { }
}
