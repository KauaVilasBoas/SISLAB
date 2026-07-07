using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Messaging;

/// <summary>
/// Implementação in-process de <see cref="IEventBus"/>.
/// Resolve os handlers do integration event via DI (IServiceProvider) e os invoca
/// em sequência no mesmo processo.
///
/// QUANDO USAR:
/// Adequado para desenvolvimento local, testes de integração e arquiteturas onde todos os
/// módulos rodam no mesmo processo. Para produção com múltiplas instâncias, substitua por
/// uma implementação baseada em broker (RabbitMQ, SQS, etc.) — a interface IEventBus
/// é o ponto de troca sem impacto nos publishers.
///
/// REGISTRO:
/// services.AddScoped&lt;IEventBus, InMemoryEventBus&gt;();
///
/// HANDLERS:
/// Registre IIntegrationEventHandler&lt;TEvent&gt; no DI; o bus os descobre via IServiceProvider.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        Type handlerType = typeof(IIntegrationEventHandler<TEvent>);
        IEnumerable<object?> handlers = _serviceProvider.GetServices(handlerType);

        foreach (object? handler in handlers)
        {
            if (handler is not IIntegrationEventHandler<TEvent> typedHandler)
                continue;

            try
            {
                await typedHandler.HandleAsync(integrationEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "InMemoryEventBus: falha no handler {HandlerType} para evento {EventType}",
                    handler.GetType().Name, typeof(TEvent).Name);

                // Não relança: outros handlers devem ter a chance de executar.
                // Em produção, considere Dead Letter Queue ou compensação.
            }
        }
    }
}
