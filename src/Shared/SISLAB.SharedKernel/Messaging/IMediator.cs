namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Dispatcher CQRS leve — resolve o handler correspondente via DI e despacha a requisição.
/// Não utiliza MediatR nem qualquer biblioteca externa; implementação em SISLAB.Infrastructure.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Despacha uma requisição para o handler registrado no contêiner de DI.
    /// </summary>
    Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default);
}
