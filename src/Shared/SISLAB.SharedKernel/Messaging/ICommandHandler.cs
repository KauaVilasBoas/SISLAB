namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Handler especializado para <see cref="ICommand"/> (sem valor de retorno).
/// Alias semântico sobre <see cref="IRequestHandler{TRequest,TResult}"/>.
/// </summary>
/// <typeparam name="TCommand">Tipo do command.</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Unit>
    where TCommand : ICommand { }

/// <summary>
/// Handler especializado para <see cref="ICommand{TResult}"/> (com valor de retorno).
/// </summary>
/// <typeparam name="TCommand">Tipo do command.</typeparam>
/// <typeparam name="TResult">Tipo do resultado.</typeparam>
public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult> { }
