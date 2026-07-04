namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Handler genérico para qualquer <see cref="IRequest{TResult}"/>.
/// Implementações são registradas no DI e resolvidas pelo <see cref="IMediator"/>.
/// </summary>
/// <typeparam name="TRequest">Tipo da requisição (command ou query).</typeparam>
/// <typeparam name="TResult">Tipo do resultado.</typeparam>
public interface IRequestHandler<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    Task<TResult> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
