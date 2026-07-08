namespace SISLAB.Modules.Identity.Infrastructure.Seeding;

/// <summary>
/// Opções do seed de desenvolvimento (empresa demo LAFTE + usuário administrador).
///
/// Vinculadas à seção de configuração <c>Seed</c>. As credenciais do admin NUNCA são
/// hardcoded: vêm de User Secrets / variáveis de ambiente (<c>Seed:Admin:*</c>). O seed
/// só executa quando <see cref="Enabled"/> = true e todas as credenciais estão presentes.
/// </summary>
public sealed class DevSeedOptions
{
    /// <summary>Nome da seção de configuração raiz.</summary>
    public const string SectionName = "Seed";

    /// <summary>
    /// Habilita a execução do seed no boot. Padrão: false (opt-in).
    /// Recomenda-se ligar apenas em ambientes de desenvolvimento.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Dados do usuário administrador demo.</summary>
    public AdminSeedOptions Admin { get; set; } = new();

    /// <summary>
    /// Indica se há credenciais suficientes para semear o admin.
    /// Evita executar o seed com configuração incompleta.
    /// </summary>
    public bool HasAdminCredentials =>
        !string.IsNullOrWhiteSpace(Admin.Email)
        && !string.IsNullOrWhiteSpace(Admin.Username)
        && !string.IsNullOrWhiteSpace(Admin.Password);

    /// <summary>Credenciais do usuário administrador semeado.</summary>
    public sealed class AdminSeedOptions
    {
        /// <summary>E-mail do admin (identificador de login).</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Username do admin.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Senha do admin. Deve respeitar a política da Lumen (mín. 12 chars, maiúscula,
        /// minúscula, dígito, caractere especial). Fornecida via User Secret/env.
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }
}
