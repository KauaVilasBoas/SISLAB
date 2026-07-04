namespace SISLAB.SharedKernel.Exceptions;

/// <summary>
/// Conflito de estado: a operação não pode ser concluída porque há uma
/// inconsistência ou duplicidade (ex.: e-mail já cadastrado). HTTP 409 Conflict.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
