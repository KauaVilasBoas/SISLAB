using System.Reflection;
using System.Text.RegularExpressions;

namespace SISLAB.Api.Tests.Authorization;

/// <summary>
/// Drift guard (card #77): every permission code the SPA gates its UI on — the frontend catalogue in
/// <c>frontend/src/modules/auth/permissions.ts</c> — must correspond to a real permission the backend
/// actually enforces with <c>[RequirePermission]</c>. A typo, or a code for an endpoint that is not in
/// fact gated, would silently hide the button/screen from EVERYONE (the client check never matches), so
/// this test fails the build instead of letting that ship.
///
/// <para>The backend truth set is, for every gated controller action (see <see cref="ControllerActionCatalog"/>):
/// the derived <c>&lt;Controller&gt;.&lt;Action&gt;</c> code AND any explicit string passed to
/// <c>[RequirePermission("...")]</c> (read from the attribute's constructor/named arguments via
/// <see cref="MemberInfo.GetCustomAttributesData"/>, since the Lumen attribute type ships in a package).
/// Together these cover both convention-based codes and feature-level ones (e.g. <c>Inventory.Cost.Read</c>).</para>
///
/// <para>Direction asserted: frontend ⊆ backend. Reads are only <c>[Authorize]</c> and legitimately carry no
/// permission code, and not every backend write needs a UI gate, so a backend code absent from the frontend
/// is not an error — only a frontend code with no backing is.</para>
/// </summary>
public sealed class FrontendPermissionCatalogDriftTests
{
    /// <summary>A permission code: PascalCase segments joined by dots, e.g. <c>Stock.RegisterEntry</c>.</summary>
    private static readonly Regex CodeShape =
        new(@"^[A-Z][A-Za-z]*(?:\.[A-Za-z]+)+$", RegexOptions.Compiled);

    [Fact]
    public void FrontendPermissionCatalogue_OnlyReferences_RealBackendPermissions()
    {
        IReadOnlySet<string> backend = BackendEnforcedCodes();
        IReadOnlyList<string> frontend = FrontendCatalogueCodes();

        // Guards a broken regex / a moved or renamed catalogue file silently passing the test.
        Assert.NotEmpty(frontend);

        List<string> drift = frontend
            .Where(code => !backend.Contains(code))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            drift.Count == 0,
            "Frontend permission codes with no backing [RequirePermission] on the backend " +
            "(typo, or gating a non-gated endpoint — the UI would be hidden from everyone): " +
            string.Join(", ", drift) +
            ". Backend enforces: " + string.Join(", ", backend.OrderBy(c => c, StringComparer.Ordinal)));
    }

    /// <summary>Sanity: the backend truth set must be non-empty (guards a silent empty controller scan).</summary>
    [Fact]
    public void Discovery_ShouldFind_EnforcedPermissions() => Assert.NotEmpty(BackendEnforcedCodes());

    private static HashSet<string> BackendEnforcedCodes()
    {
        HashSet<string> codes = new(StringComparer.Ordinal);

        foreach (ControllerAction action in ControllerActionCatalog.All.Where(a => a.HasRequirePermission))
        {
            // Convention: parameterless [RequirePermission] materializes <Controller>.<Action>.
            codes.Add(action.PermissionCode);

            // Explicit: [RequirePermission("Feature.Code")] — read the actual argument(s), not the derived name.
            foreach (CustomAttributeData data in action.Method.GetCustomAttributesData())
            {
                if (data.AttributeType.Name != "RequirePermissionAttribute")
                    continue;

                IEnumerable<object?> values = data.ConstructorArguments
                    .Select(a => a.Value)
                    .Concat(data.NamedArguments.Select(a => a.TypedValue.Value));

                foreach (object? value in values)
                    if (value is string s && CodeShape.IsMatch(s))
                        codes.Add(s);
            }
        }

        return codes;
    }

    private static IReadOnlyList<string> FrontendCatalogueCodes()
    {
        string text = File.ReadAllText(LocatePermissionsFile());

        // The only single-quoted, code-shaped literals in permissions.ts are the permission code values.
        return Regex.Matches(text, @"'([A-Z][A-Za-z]*(?:\.[A-Za-z]+)+)'")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string LocatePermissionsFile()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SISLAB.sln")))
            dir = dir.Parent;

        Assert.True(dir is not null, "Could not locate the repository root (SISLAB.sln) from the test runtime.");

        string file = Path.Combine(
            dir!.FullName, "frontend", "src", "modules", "auth", "permissions.ts");
        Assert.True(File.Exists(file), $"Frontend permission catalogue not found at {file}.");
        return file;
    }
}
