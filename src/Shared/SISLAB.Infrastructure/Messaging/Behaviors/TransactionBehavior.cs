using SISLAB.Infrastructure.Persistence;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Messaging.Behaviors;

/// <summary>
/// Pipeline behavior that wraps a <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>
/// handler in a transaction via <see cref="IUnitOfWork"/>.
///
/// For Commands: calls SaveChangesAsync after the handler succeeds. Exceptions propagate
/// naturally — EF discards in-memory changes when the transaction is not committed.
/// For Queries (IQuery): no-op — queries must not trigger write transactions.
///
/// EF Core does not use explicit transactions by default — each SaveChangesAsync is atomic.
/// Explicit IDbContextTransaction is only needed when multiple SaveChanges calls must be
/// grouped in a single transaction.
///
/// Pipeline order: ValidationBehavior → LoggingBehavior → TransactionBehavior → Handler
/// </summary>
public sealed class TransactionBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken = default)
    {
        // Queries must not trigger SaveChanges — bypass the behavior.
        if (request is not (ICommand or ICommand<TResult>))
            return await next();

        TResult result = await next();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return result;
    }
}
