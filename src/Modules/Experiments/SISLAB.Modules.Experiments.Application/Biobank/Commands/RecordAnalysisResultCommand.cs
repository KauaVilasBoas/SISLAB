using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Biobank.Commands;

/// <summary>
/// Records the result of a pending analysis and signs it off as completed (card [E11] #89). The consumed aliquot
/// is fixed at analysis creation and is not touched here — only the free-text result is recorded and the analysis
/// moves to <see cref="AnalysisStatus.Completed"/>.
/// </summary>
public sealed record RecordAnalysisResultCommand(
    Guid SampleId,
    Guid AnalysisId,
    string Result) : ICommand;

internal sealed class RecordAnalysisResultCommandValidator : AbstractValidator<RecordAnalysisResultCommand>
{
    public RecordAnalysisResultCommandValidator()
    {
        RuleFor(command => command.SampleId).NotEmpty();
        RuleFor(command => command.AnalysisId).NotEmpty();
        RuleFor(command => command.Result).NotEmpty().MaximumLength(4000);
    }
}

internal sealed class RecordAnalysisResultCommandHandler : ICommandHandler<RecordAnalysisResultCommand>
{
    private readonly ISampleRepository _samples;

    public RecordAnalysisResultCommandHandler(ISampleRepository samples) => _samples = samples;

    public async Task<Unit> HandleAsync(
        RecordAnalysisResultCommand request,
        CancellationToken cancellationToken = default)
    {
        Sample sample = await _samples.FindByIdAsync(request.SampleId, cancellationToken)
            ?? throw new NotFoundException($"Sample '{request.SampleId}' was not found.");

        sample.RecordAnalysisResult(request.AnalysisId, request.Result);

        await _samples.UpdateAsync(sample, cancellationToken);

        return Unit.Value;
    }
}
