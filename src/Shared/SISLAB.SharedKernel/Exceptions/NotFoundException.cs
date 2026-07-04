namespace SISLAB.SharedKernel.Exceptions;

/// <summary>
/// Recurso não encontrado. Resulta em HTTP 404 Not Found.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string resourceName, object key)
        : base($"'{resourceName}' com identificador '{key}' não foi encontrado.") { }

    public NotFoundException(string message) : base(message) { }
}
