using SISLAB.Infrastructure.Persistence;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Messaging.Behaviors;

/// <summary>
/// Behavior de pipeline que envolve o handling de um <see cref="ICommand"/> ou
/// <see cref="ICommand{TResult}"/> em uma transação via <see cref="IUnitOfWork"/>.
///
/// COMPORTAMENTO:
/// - Para Commands: chama SaveChangesAsync após o handler com sucesso.
///   Exceções propagam naturalmente — EF reverte as mudanças em memória quando a
///   transação não é commitada.
/// - Para Queries (IQuery): o behavior é no-op — queries não devem iniciar transações de escrita.
///
/// NOTA SOBRE ROLLBACK:
/// O EF Core não usa transações explícitas por padrão — cada SaveChangesAsync é atômico.
/// Se uma exceção ocorrer NO HANDLER antes do SaveChangesAsync, as mudanças ficam apenas
/// em memória e são descartadas quando o DbContext sai de escopo. Se a exceção ocorrer
/// DURANTE o SaveChangesAsync (ex.: violação de constraint), o próprio SaveChanges aborta.
/// Portanto, o rollback explícito só é necessário se você precisar envolver múltiplos
/// SaveChanges na mesma transação — neste caso, use IDbContextTransaction diretamente.
///
/// ORDEM NO PIPELINE: ValidationBehavior → LoggingBehavior → TransactionBehavior → Handler
/// </summary>
/// <typeparam name="TRequest">Tipo do request sendo processado.</typeparam>
/// <typeparam name="TResult">Tipo do resultado.</typeparam>
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
        // Queries não devem acionar SaveChanges — bypassa o behavior.
        if (request is not (ICommand or ICommand<TResult>))
            return await next();

        TResult result = await next();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return result;
    }
}
