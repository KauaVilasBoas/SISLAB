namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Handler especializado para <see cref="IQuery{TResult}"/>.
/// Alias semântico sobre <see cref="IRequestHandler{TRequest,TResult}"/>.
/// </summary>
/// <typeparam name="TQuery">Tipo da query.</typeparam>
/// <typeparam name="TResult">Tipo do resultado.</typeparam>
public interface IQueryHandler<in TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult> { }
