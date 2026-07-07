using FluentValidation;
using FluentValidation.Results;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Messaging.Behaviors;

/// <summary>
/// Behavior de pipeline que executa validadores FluentValidation antes do handler.
/// Se nenhum <see cref="IValidator{TRequest}"/> estiver registrado, o request passa direto.
/// Se houver violações, lança <see cref="ValidationException"/> — o handler nunca é chamado.
///
/// REGISTRO:
/// O behavior é registrado automaticamente como open-generic pelo assembly scanning
/// em <see cref="SISLAB.Infrastructure.DependencyInjection.InfrastructureServiceExtensions"/>.
/// Validators são descobertos via FluentValidation.DependencyInjectionExtensions por módulo.
///
/// ORDEM NO PIPELINE: ValidationBehavior → LoggingBehavior → TransactionBehavior → Handler
/// (registrado primeiro = executa mais externamente na cadeia)
/// </summary>
/// <typeparam name="TRequest">Tipo do request sendo validado.</typeparam>
/// <typeparam name="TResult">Tipo do resultado.</typeparam>
public sealed class ValidationBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <inheritdoc />
    public async Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken = default)
    {
        if (!_validators.Any())
            return await next();

        ValidationContext<TRequest> context = new(request);

        ValidationResult[] results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        List<ValidationFailure> failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
