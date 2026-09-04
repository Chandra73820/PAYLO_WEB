using System.Security.Claims;
using System.Security.Cryptography;

namespace PAYLO_WEB.Helpers
{
    public class ClaimsHelper
    {
        public static string GetUserId(ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        public static string? GetAccessToken(ClaimsPrincipal user)
            => user.FindFirst("AccessToken")?.Value;

        public static string? GetSessionId(ClaimsPrincipal user)
            => user.FindFirst("SessionId")?.Value;

        public static string? GetRegID(ClaimsPrincipal user)
            => user.FindFirst("RegID")?.Value;

        public static string? GetMemberID(ClaimsPrincipal user)
            => user.FindFirst("MemberID")?.Value;

        public static string? GetName(ClaimsPrincipal user)
            => user.FindFirst(ClaimTypes.Name)?.Value;

        public static DateTime? GetAccessTokenExpiry(ClaimsPrincipal user)
        {
            var val = user.FindFirst("AccessTokenExpiry")?.Value;
            return DateTime.TryParse(val, out var dt) ? dt : null;
        }

        // IsTokenExpiringSoon — used by popup warning threshold (NOT by filter anymore)
        public static bool IsTokenExpiringSoon(ClaimsPrincipal user,
            int thresholdMinutes = 5)
        {
            var expiry = GetAccessTokenExpiry(user);
            if (expiry == null) return true;
            return expiry.Value.ToUniversalTime()
                <= DateTime.UtcNow.AddMinutes(thresholdMinutes);
        }


        public static bool IsTokenExpired(ClaimsPrincipal user)
        {
            var expiry = GetAccessTokenExpiry(user);
            if (expiry == null) return true;
            // ✅ Grace period of 1 minute — handles clock skew between servers
            return expiry.Value.ToUniversalTime() < DateTime.UtcNow.AddMinutes(-1);
        }
        public static string? GetRefreshToken(ClaimsPrincipal user)
    => user.FindFirst("RefreshToken")?.Value;
        public static string MakeRegPwds(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            char[] password = new char[length];

            for (int i = 0; i < length; i++)
            {
                password[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            }

            return new string(password);
        }
    }
}
