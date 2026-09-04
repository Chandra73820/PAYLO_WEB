// =============================================================================
// SchemeRegistry.cs — fixed, exposes AllConfigs directly
// =============================================================================
namespace PAYLO_WEB.Auth
{
    public static class SchemeRegistry
    {
        // ── Add new modules here only ─────────────────────────────────────────
        private static readonly Dictionary<string, SchemeConfig> _schemes = new(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = new("AdminScheme", "/Admin/Login", "/api/Admin"),
        };

        // ✅ Get config by UserType ("Admin", "Associate" etc.)
        public static SchemeConfig? Get(string? userType)
            => string.IsNullOrEmpty(userType) ? null
             : _schemes.TryGetValue(userType, out var cfg) ? cfg : null;

        // ✅ Get config with fallback to Admin
        public static SchemeConfig GetOrDefault(string? userType)
            => Get(userType) ?? _schemes["Admin"];

        // ✅ Get config by area name (matches LoginPath prefix)
        public static SchemeConfig? GetByArea(string? area)
            => string.IsNullOrEmpty(area) ? null
             : _schemes.Values.FirstOrDefault(s =>
                   s.LoginPath.StartsWith($"/{area}/",
                       StringComparison.OrdinalIgnoreCase));

        // ✅ All configs — used by Program.cs loop (no lookup confusion)
        public static IEnumerable<SchemeConfig> AllConfigs
            => _schemes.Values;

        // All scheme names — used where only names needed
        public static IEnumerable<string> AllSchemeNames
            => _schemes.Values.Select(s => s.SchemeName);

        // Common API prefix for shared endpoints
        public const string CommonApiPrefix = "/api/Common";
    }

    public record SchemeConfig(
        string SchemeName,   // "AdminScheme"
        string LoginPath,    // "/Admin/Login"
        string ApiPrefix);   // "/api/Admin"
}