namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Marcador base para requisições (commands e queries) despachadas pelo mediator.
/// </summary>
/// <typeparam name="TResult">Tipo do resultado da requisição.</typeparam>
public interface IRequest<TResult> { }
