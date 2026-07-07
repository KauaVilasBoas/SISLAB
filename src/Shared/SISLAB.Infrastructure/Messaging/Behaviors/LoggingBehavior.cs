using Microsoft.Extensions.Logging;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Messaging.Behaviors;

/// <summary>
/// Behavior de pipeline que registra logs estruturados de início, fim e duração de cada request.
/// Inclui o nome do request como campo de log para correlação e análise de performance.
///
/// LOG ESTRUTURADO:
/// - Início: LogDebug com nome do request.
/// - Sucesso: LogDebug com nome do request e duração em ms.
/// - Falha: LogWarning com nome do request, duração e tipo da exceção.
///
/// A exceção é re-lançada após o log — este behavior nunca engole erros.
///
/// ORDEM NO PIPELINE: ValidationBehavior → LoggingBehavior → TransactionBehavior → Handler
/// </summary>
/// <typeparam name="TRequest">Tipo do request sendo processado.</typeparam>
/// <typeparam name="TResult">Tipo do resultado.</typeparam>
public sealed class LoggingBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResult>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResult>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken = default)
    {
        string requestName = typeof(TRequest).Name;

        _logger.LogDebug("Mediator: iniciando {RequestName}", requestName);

        long startedAt = Environment.TickCount64;

        try
        {
            TResult result = await next();

            long elapsed = Environment.TickCount64 - startedAt;
            _logger.LogDebug("Mediator: {RequestName} concluído em {ElapsedMs}ms", requestName, elapsed);

            return result;
        }
        catch (Exception ex)
        {
            long elapsed = Environment.TickCount64 - startedAt;
            _logger.LogWarning(
                "Mediator: {RequestName} falhou após {ElapsedMs}ms | {ExceptionType}: {Message}",
                requestName, elapsed, ex.GetType().Name, ex.Message);

            throw;
        }
    }
}
