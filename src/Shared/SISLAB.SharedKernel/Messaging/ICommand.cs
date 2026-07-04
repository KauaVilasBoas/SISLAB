namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Marcador para commands que não retornam valor de negócio.
/// Commands mudam estado; nunca retornam dados de leitura.
/// </summary>
public interface ICommand : IRequest<Unit> { }

/// <summary>
/// Marcador para commands que retornam um valor (ex.: ID do recurso criado).
/// </summary>
/// <typeparam name="TResult">Tipo do resultado, geralmente um ID ou status.</typeparam>
public interface ICommand<TResult> : IRequest<TResult> { }
