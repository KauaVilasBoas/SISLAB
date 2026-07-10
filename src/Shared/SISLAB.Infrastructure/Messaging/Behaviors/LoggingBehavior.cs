using Microsoft.Extensions.Logging;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Messaging.Behaviors;

/// <summary>
/// Pipeline behavior that emits structured logs for request start, completion, and duration.
/// Exceptions are re-thrown after logging — this behavior never swallows errors.
///
/// Pipeline order: ValidationBehavior → LoggingBehavior → TransactionBehavior → Handler
/// </summary>
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

        _logger.LogDebug("Mediator: starting {RequestName}", requestName);

        long startedAt = Environment.TickCount64;

        try
        {
            TResult result = await next();

            long elapsed = Environment.TickCount64 - startedAt;
            _logger.LogDebug("Mediator: {RequestName} completed in {ElapsedMs}ms", requestName, elapsed);

            return result;
        }
        catch (Exception ex)
        {
            long elapsed = Environment.TickCount64 - startedAt;
            _logger.LogWarning(
                "Mediator: {RequestName} failed after {ElapsedMs}ms | {ExceptionType}: {Message}",
                requestName, elapsed, ex.GetType().Name, ex.Message);

            throw;
        }
    }
}
