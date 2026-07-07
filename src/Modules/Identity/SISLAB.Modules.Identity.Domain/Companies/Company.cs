using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Identity.Domain.Companies;

/// <summary>
/// Aggregate root que representa uma empresa (tenant) no SISLAB.
///
/// Uma Company agrupa usuários da Lumen (referenciados por valor via <see cref="CompanyMembership"/>)
/// e serve de escopo para a autorização granular da Lumen.Authorization.
///
/// Invariantes:
/// - Nome não pode ser nulo nem vazio.
/// - Não é possível adicionar o mesmo usuário duas vezes à mesma empresa.
/// - Um usuário pode pertencer a múltiplas empresas (N:N via CompanyMembership).
/// </summary>
public sealed class Company : AggregateRoot<Guid>
{
    private readonly List<CompanyMembership> _memberships = [];

    // Construtor privado para EF Core
    private Company() : base(Guid.Empty) { }

    private Company(Guid id, string name, string? taxId, DateTime createdAt)
        : base(id)
    {
        Name = name;
        TaxId = taxId;
        CreatedAt = createdAt;
        IsActive = true;
    }

    /// <summary>Nome fantasia ou razão social da empresa.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>CNPJ ou identificador fiscal da empresa (opcional).</summary>
    public string? TaxId { get; private set; }

    /// <summary>Indica se a empresa está ativa. Empresas inativas não podem logar.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Data de criação do registro.</summary>
    public DateTime CreatedAt { get; private init; }

    /// <summary>Membros da empresa. Somente-leitura externamente.</summary>
    public IReadOnlyList<CompanyMembership> Memberships => _memberships.AsReadOnly();

    /// <summary>
    /// Cria uma nova empresa com os dados informados.
    /// </summary>
    public static Company Create(string name, string? taxId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da empresa não pode ser vazio.", nameof(name));

        return new Company(Guid.NewGuid(), name.Trim(), taxId?.Trim(), DateTime.UtcNow);
    }

    /// <summary>
    /// Atualiza o nome da empresa.
    /// </summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("O novo nome não pode ser vazio.", nameof(newName));

        Name = newName.Trim();
    }

    /// <summary>
    /// Ativa a empresa.
    /// </summary>
    public void Activate() => IsActive = true;

    /// <summary>
    /// Desativa a empresa. Usuários associados perdem acesso implicitamente via filtro de tenant.
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Associa um usuário da Lumen a esta empresa.
    /// O userId é o ID do usuário no sistema de identidade externo (Lumen), referenciado por valor.
    /// </summary>
    /// <param name="lumenUserId">ID do usuário na Lumen.</param>
    /// <exception cref="InvalidOperationException">Usuário já é membro desta empresa.</exception>
    public void AddMember(Guid lumenUserId)
    {
        bool alreadyMember = _memberships.Any(m => m.LumenUserId == lumenUserId);
        if (alreadyMember)
            throw new InvalidOperationException(
                $"Usuário '{lumenUserId}' já é membro da empresa '{Name}'.");

        _memberships.Add(CompanyMembership.Create(Id, lumenUserId));
    }

    /// <summary>
    /// Remove a associação de um usuário da Lumen com esta empresa.
    /// </summary>
    /// <param name="lumenUserId">ID do usuário na Lumen.</param>
    /// <exception cref="InvalidOperationException">Usuário não é membro desta empresa.</exception>
    public void RemoveMember(Guid lumenUserId)
    {
        CompanyMembership? membership = _memberships.FirstOrDefault(m => m.LumenUserId == lumenUserId);
        if (membership is null)
            throw new InvalidOperationException(
                $"Usuário '{lumenUserId}' não é membro da empresa '{Name}'.");

        _memberships.Remove(membership);
    }

    /// <summary>
    /// Reconstitui uma empresa a partir do repositório (usado pelo EF Core via navigation loading).
    /// </summary>
    internal void LoadMemberships(IEnumerable<CompanyMembership> memberships)
    {
        _memberships.Clear();
        _memberships.AddRange(memberships);
    }
}
