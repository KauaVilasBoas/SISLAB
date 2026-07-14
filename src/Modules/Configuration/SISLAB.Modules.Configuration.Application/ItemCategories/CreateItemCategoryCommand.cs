using FluentValidation;
using SISLAB.Modules.Configuration.Domain.ItemCategories;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application.ItemCategories;

/// <summary>
/// Creates a new item category for the active company (card [E12] #76) — the dynamic, per-tenant replacement
/// for the retired <c>StockItemCategory</c> enum. Write-side: it guards the name's uniqueness within the
/// tenant, builds the aggregate through its factory (which owns the name/alias invariants) and lets the unit
/// of work commit. Returns the new category id, which the Inventory module references by value.
/// </summary>
public sealed record CreateItemCategoryCommand(
    string Name,
    IReadOnlyList<string>? Aliases,
    bool IsControlled) : ICommand<Guid>;

internal sealed class CreateItemCategoryCommandValidator : AbstractValidator<CreateItemCategoryCommand>
{
    public CreateItemCategoryCommandValidator()
        => RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
}

internal sealed class CreateItemCategoryCommandHandler : ICommandHandler<CreateItemCategoryCommand, Guid>
{
    private readonly IItemCategoryRepository _categories;

    public CreateItemCategoryCommandHandler(IItemCategoryRepository categories) => _categories = categories;

    public async Task<Guid> HandleAsync(
        CreateItemCategoryCommand request,
        CancellationToken cancellationToken = default)
    {
        ItemCategory? existing = await _categories.FindByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"A category named '{request.Name.Trim()}' already exists.");

        ItemCategory category = ItemCategory.Create(request.Name, request.Aliases, request.IsControlled);
        await _categories.AddAsync(category, cancellationToken);

        return category.Id;
    }
}
