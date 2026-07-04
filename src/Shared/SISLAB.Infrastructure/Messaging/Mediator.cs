using SISLAB.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace SISLAB.Infrastructure.Messaging;

/// <summary>
/// Implementação leve do <see cref="IMediator"/> — dispatcher CQRS via DI.
/// Resolve o handler correspondente ao tipo da requisição no IServiceProvider
/// e delega a execução. Sem dependência de MediatR ou qualquer biblioteca externa.
/// </summary>
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Type requestType = request.GetType();
        Type handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResult));

        object handler = _serviceProvider.GetRequiredService(handlerType);

        // Invocação via reflexão: handler é resolvido dinamicamente pelo tipo de TRequest.
        // O custo de reflexão por request é aceitável para o volume do SISLAB;
        // se tornar gargalo no futuro, substituir por cache de delegates compilados.
        var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResult>, TResult>.HandleAsync))!;

        return (Task<TResult>)handleMethod.Invoke(handler, [request, cancellationToken])!;
    }
}
