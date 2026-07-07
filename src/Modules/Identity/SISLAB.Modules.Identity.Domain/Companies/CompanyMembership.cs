using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Identity.Domain.Companies;

/// <summary>
/// Entidade de associação N:N entre uma <see cref="Company"/> e um usuário da Lumen.
///
/// O userId da Lumen é armazenado por valor (<see cref="LumenUserId"/>) — sem FK,
/// sem navegação para tabelas da Lumen. Isso garante o isolamento entre os bounded contexts:
/// o SISLAB não conhece o schema interno do sistema de identidade.
///
/// Uma <see cref="CompanyMembership"/> pertence ao agregado <see cref="Company"/>
/// e não deve ser modificada fora dele.
/// </summary>
public sealed class CompanyMembership : Entity<Guid>
{
    // Construtor privado para EF Core
    private CompanyMembership() : base(Guid.Empty) { }

    private CompanyMembership(Guid id, Guid companyId, Guid lumenUserId, DateTime joinedAt)
        : base(id)
    {
        CompanyId = companyId;
        LumenUserId = lumenUserId;
        JoinedAt = joinedAt;
    }

    /// <summary>ID da empresa à qual o usuário pertence.</summary>
    public Guid CompanyId { get; private init; }

    /// <summary>
    /// ID do usuário no sistema de identidade externo (Lumen Identity).
    /// Referenciado por valor — sem FK para evitar acoplamento de schema cross-boundary.
    /// </summary>
    public Guid LumenUserId { get; private init; }

    /// <summary>Data em que o usuário ingressou na empresa.</summary>
    public DateTime JoinedAt { get; private init; }

    /// <summary>
    /// Cria uma nova associação entre empresa e usuário.
    /// </summary>
    internal static CompanyMembership Create(Guid companyId, Guid lumenUserId)
        => new(Guid.NewGuid(), companyId, lumenUserId, DateTime.UtcNow);
}
