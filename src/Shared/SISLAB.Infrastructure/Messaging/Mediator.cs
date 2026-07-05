using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Messaging;

/// <summary>
/// Implementação do <see cref="IMediator"/> com suporte a pipeline de behaviors.
///
/// PIPELINE DE EXECUÇÃO (da ordem de registro no DI):
/// ValidationBehavior → LoggingBehavior → TransactionBehavior → IRequestHandler
///
/// RESOLUÇÃO POR REFLEXÃO:
/// O tipo concreto de TRequest é conhecido apenas em runtime (polimorfismo no SendAsync).
/// Usamos reflexão para construir o pipeline com os tipos corretos e cacheamos o método
/// invocador em um ConcurrentDictionary para evitar o overhead de reflexão repetida.
///
/// BEHAVIORS OPEN-GENERIC:
/// Behaviors registrados como IPipelineBehavior&lt;&gt; (open-generic) são resolvidos
/// como IEnumerable&lt;IPipelineBehavior&lt;TRequest, TResult&gt;&gt; via DI e encadeados em ordem.
/// </summary>
public sealed class Mediator : IMediator
{
    // Cache de MethodInfo para o método genérico interno — evita GetMethod por request.
    private static readonly ConcurrentDictionary<Type, MethodInfo> DispatchCache = new();

    private readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Obtém o tipo concreto em runtime (ex: CreateItemCommand) para resolver handlers corretos.
        Type requestType = request.GetType();

        MethodInfo dispatchMethod = DispatchCache.GetOrAdd(
            requestType,
            static rt => typeof(Mediator)
                .GetMethod(nameof(DispatchAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(rt, typeof(TResult)));

        return (Task<TResult>)dispatchMethod.Invoke(this, [request, cancellationToken])!;
    }

    /// <summary>
    /// Método genérico interno que constrói e executa o pipeline completo.
    /// TRequest e TResult são conhecidos em compile-time aqui — behaviors são resolvidos corretamente.
    /// </summary>
    private async Task<TResult> DispatchAsync<TRequest, TResult>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResult>
    {
        // Resolve o handler terminal.
        IRequestHandler<TRequest, TResult> handler =
            _serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResult>>();

        // Resolve todos os behaviors registrados para este par (TRequest, TResult).
        IEnumerable<IPipelineBehavior<TRequest, TResult>> behaviors =
            _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResult>>();

        // Constrói a cadeia de delegates de fora para dentro (último registered = mais interno).
        RequestHandlerDelegate<TResult> pipeline = behaviors
            .Reverse()
            .Aggregate(
                seed: (RequestHandlerDelegate<TResult>)(() =>
                    handler.HandleAsync(request, cancellationToken)),
                func: (next, behavior) =>
                    () => behavior.HandleAsync(request, next, cancellationToken));

        return await pipeline();
    }
}
