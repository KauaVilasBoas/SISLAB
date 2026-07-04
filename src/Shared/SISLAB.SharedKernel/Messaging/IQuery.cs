namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Marcador para queries que lêem estado sem mudar nada.
/// </summary>
/// <typeparam name="TResult">Tipo do resultado da consulta.</typeparam>
public interface IQuery<TResult> : IRequest<TResult> { }
