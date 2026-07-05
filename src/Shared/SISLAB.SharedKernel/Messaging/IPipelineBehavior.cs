namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Comportamento de pipeline que envolve a execução de um <see cref="IRequestHandler{TRequest,TResult}"/>.
/// Behaviors são encadeados em ordem de registro e executados antes/depois do handler.
///
/// Padrão de implementação:
/// <code>
/// public async Task&lt;TResult&gt; HandleAsync(TRequest request, RequestHandlerDelegate&lt;TResult&gt; next, CancellationToken ct)
/// {
///     // pré-processamento
///     var result = await next();
///     // pós-processamento
///     return result;
/// }
/// </code>
/// </summary>
/// <typeparam name="TRequest">Tipo da requisição (command ou query).</typeparam>
/// <typeparam name="TResult">Tipo do resultado.</typeparam>
public interface IPipelineBehavior<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    /// <summary>
    /// Executa a lógica do behavior e chama o próximo elemento do pipeline via <paramref name="next"/>.
    /// </summary>
    /// <param name="request">A requisição sendo processada.</param>
    /// <param name="next">Delegate para o próximo passo do pipeline (ou o handler final).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Delegate que representa o próximo passo no pipeline de behaviors.
/// </summary>
/// <typeparam name="TResult">Tipo do resultado.</typeparam>
public delegate Task<TResult> RequestHandlerDelegate<TResult>();
