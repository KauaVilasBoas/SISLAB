namespace SISLAB.Modules.Identity.Infrastructure.Seeding;

/// <summary>
/// Options for the development seed (LAFTE demo company + admin user).
///
/// Bound to the <c>Seed</c> configuration section. Admin credentials are NEVER hardcoded —
/// they come from User Secrets / environment variables (<c>Seed:Admin:*</c>). The seed only
/// runs when <see cref="Enabled"/> = true and all credentials are present.
/// </summary>
public sealed class DevSeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// Enables the seed on startup. Default: false (opt-in).
    /// Should only be enabled in development environments.
    /// </summary>
    public bool Enabled { get; set; }

    public AdminSeedOptions Admin { get; set; } = new();

    /// <summary>
    /// Returns true when sufficient credentials exist to seed the admin user.
    /// Prevents the seed from running with incomplete configuration.
    /// </summary>
    public bool HasAdminCredentials =>
        !string.IsNullOrWhiteSpace(Admin.Email)
        && !string.IsNullOrWhiteSpace(Admin.Username)
        && !string.IsNullOrWhiteSpace(Admin.Password);

    public sealed class AdminSeedOptions
    {
        public string Email { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Admin password. Must satisfy Lumen's policy (min. 12 chars: uppercase, lowercase,
        /// digit, special character). Provided via User Secret / environment variable.
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }
}
