// =============================================================================
// AuthService.cs — SchemeRegistry-based, handles all user types dynamically
// =============================================================================
using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json;
using PAYLO_Classes.Common;
using PAYLO_WEB.Auth;
using PAYLO_WEB.Helpers;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;

namespace PAYLO_WEB.Services
{
    public interface IAuthService
    {
        // scheme tells service exactly which user to refresh
        Task<TokenRefreshResult> RefreshTokenIfNeededAsync(
            HttpContext httpContext, string scheme);

        Task SignOutAsync(HttpContext httpContext, string scheme);
        // ✅ ADD THESE TWO
        Task SoftRevokeAsync(HttpContext httpContext, string scheme);
        Task CancelSoftRevokeAsync(HttpContext httpContext, string scheme);
    }

    public class AuthService : IAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AuthService> _logger;
        private readonly string _apiBaseUrl;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim>
            _userLocks = new();

        private static SemaphoreSlim GetUserLock(string regId, string userType)
            => _userLocks.GetOrAdd($"{regId}_{userType}", _ => new SemaphoreSlim(1, 1));

        public AuthService(IHttpClientFactory factory,
                           ILogger<AuthService> logger,
                           IConfiguration config)
        {
            _httpClientFactory = factory;
            _logger = logger;
            _apiBaseUrl = config.GetValue<string>("ApiUrl")!;
        }

        // ── Refresh token if expiring soon ────────────────────────────────────
        public async Task<TokenRefreshResult> RefreshTokenIfNeededAsync(
            HttpContext httpContext, string scheme)
        {
            // ✅ Authenticate with SPECIFIC scheme — not httpContext.User (default Admin)
            var authResult = await httpContext.AuthenticateAsync(scheme);
            if (!authResult.Succeeded)
                return TokenRefreshResult.NotNeeded;

            var user = authResult.Principal!;

            // ✅ Threshold matches WARNING_BEFORE_MS (5 min) in layouts
            if (!ClaimsHelper.IsTokenExpiringSoon(user, thresholdMinutes: 5))
                return TokenRefreshResult.NotNeeded;

            var userType = user.FindFirst("UserType")?.Value;
            var regId = ClaimsHelper.GetRegID(user) ?? "0";
            var accessToken = ClaimsHelper.GetAccessToken(user);
            var refreshToken = ClaimsHelper.GetRefreshToken(user);

            var cfg = SchemeRegistry.Get(userType);
            if (cfg == null || string.IsNullOrEmpty(refreshToken))
                return TokenRefreshResult.Failed;

            var lockKey = $"{regId}_{userType}";
            var userLock = GetUserLock(regId, userType!);

            if (!await userLock.WaitAsync(TimeSpan.FromSeconds(3)))
                return TokenRefreshResult.NotNeeded;

            try
            {
                // Re-check after lock — another request may have already rotated
                var recheckAuth = await httpContext.AuthenticateAsync(scheme);
                if (!recheckAuth.Succeeded) return TokenRefreshResult.NotNeeded;
                if (!ClaimsHelper.IsTokenExpiringSoon(
                        recheckAuth.Principal!, thresholdMinutes: 5))
                    return TokenRefreshResult.NotNeeded;

                var client = _httpClientFactory.CreateClient("ApiClient");
                client.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(accessToken))
                    client.DefaultRequestHeaders.Add("Authorization",
                        $"Bearer {accessToken}");

                var endpoint = $"{_apiBaseUrl}{cfg.ApiPrefix}/RefreshToken";
                var body = JsonConvert.SerializeObject(new
                {
                    RawRefreshToken = refreshToken,
                    MultiSessionId = ClaimsHelper.GetSessionId(user)
                });

                // ✅ 10 second timeout — enough for API round trip on staging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await client.PostAsync(
                    endpoint,
                    new StringContent(body, Encoding.UTF8, "application/json"),
                    cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Refresh failed. Scheme={Scheme} Status={Status}",
                        scheme, response.StatusCode);
                    await SignOutAsync(httpContext, scheme);
                    return TokenRefreshResult.Failed;
                }

                var json = await response.Content.ReadAsStringAsync(cts.Token);
                var result = JsonConvert.DeserializeObject<RefreshTokenResponse>(json);
                if (result == null) return TokenRefreshResult.Failed;

                // ✅ Pass scheme — UpdateClaimsAsync reads correct principal
                await UpdateClaimsAsync(
                    httpContext, scheme,
                    result.AccessToken,
                    result.AccessTokenExpiry,
                    result.RefreshToken);

                _logger.LogInformation(
                    "Token refreshed. Scheme={Scheme} RegID={RegId}",
                    scheme, regId);

                return TokenRefreshResult.Refreshed;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Refresh timeout. Scheme={Scheme}", scheme);
                return TokenRefreshResult.NotNeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh error. Scheme={Scheme}", scheme);
                return TokenRefreshResult.Failed;
            }
            finally
            {
                userLock.Release();
                if (userLock.CurrentCount == 1)
                    _userLocks.TryRemove(lockKey, out _);
            }
        }

        // ── Sign out ──────────────────────────────────────────────────────────
        public async Task SignOutAsync(HttpContext httpContext, string scheme)
        {
            try
            {
                // ✅ AuthenticateAsync reads cookie directly
                // Works even when HttpContext.User is anonymous (token expired at 00:00)
                // 30s grace period in OnValidatePrincipal keeps cookie valid
                var authResult = await httpContext.AuthenticateAsync(scheme);
                var principal = authResult.Succeeded
                                     ? authResult.Principal
                                     : httpContext.User;   // fallback

                var userType = principal?.FindFirst("UserType")?.Value;
                var accessToken = ClaimsHelper.GetAccessToken(principal);
                var refreshToken = ClaimsHelper.GetRefreshToken(principal);
                var cfg = SchemeRegistry.Get(userType);

                if (cfg != null && !string.IsNullOrEmpty(refreshToken))
                {
                    var client = _httpClientFactory.CreateClient("ApiClient");
                    client.DefaultRequestHeaders.Clear();
                    if (!string.IsNullOrEmpty(accessToken))
                        client.DefaultRequestHeaders.Add("Authorization",
                            $"Bearer {accessToken}");

                    var body = JsonConvert.SerializeObject(new
                    {
                        RawRefreshToken = refreshToken,
                        RawJwt = accessToken ?? ""
                    });

                    // ✅ Await directly — no Task.Run (Task.Run killed by IIS in-process)
                    // 5 second timeout — if exceeded: cookie still cleared, nightly purge handles RT
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await client.PostAsync(
                            $"{_apiBaseUrl}{cfg.ApiPrefix}/Logout",
                            new StringContent(body, Encoding.UTF8, "application/json"),
                            cts.Token);

                        _logger.LogInformation(
                            "Logout API called. Scheme={Scheme}", scheme);
                    }
                    catch (OperationCanceledException)
                    {
                        // ✅ Expected on slow network or tab close — not an error
                        _logger.LogDebug(
                            "Logout API timeout (5s). Scheme={Scheme}", scheme);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Logout API failed. Scheme={Scheme}", scheme);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignOutAsync error. Scheme={Scheme}", scheme);
            }
            finally
            {
                // ✅ Always clear cookie — even if API call failed or timed out
                await httpContext.SignOutAsync(scheme);
            }
        }

        // ── Update MVC claims after token rotation ────────────────────────────
        private static async Task UpdateClaimsAsync(
            HttpContext httpContext,
            string scheme,
            string newAccessToken,
            string newAccessTokenExpiry,   // already ISO "o" format
            string newRefreshToken)
        {
            // ✅ Authenticate with SPECIFIC scheme to get current claims
            // httpContext.User = default scheme (Admin) — wrong for Associate/Franchise
            var authResult = await httpContext.AuthenticateAsync(scheme);
            var currentPrincipal = authResult.Succeeded
                                       ? authResult.Principal
                                       : httpContext.User;

            // Preserve all existing claims except the three being rotated
            var existing = (currentPrincipal?.Claims ?? Enumerable.Empty<Claim>())
                .Where(c => c.Type != "AccessToken"
                         && c.Type != "AccessTokenExpiry"
                         && c.Type != "RefreshToken")
                .ToList();

            existing.Add(new Claim("AccessToken", newAccessToken));
            existing.Add(new Claim("AccessTokenExpiry", newAccessTokenExpiry));
            existing.Add(new Claim("RefreshToken", newRefreshToken));

            var identity = new ClaimsIdentity(existing, scheme);
            var principal = new ClaimsPrincipal(identity);

            await httpContext.SignInAsync(scheme, principal,
                new AuthenticationProperties { IsPersistent = false });
        }

        #region SoftRevoke

        // ADD TO AuthService class — paste after SignOutAsync, before UpdateClaimsAsync


        // ── Soft revoke — called on pagehide (tab/browser close OR reload) ────
        // Marks the token PendingRevoke=1 on the API side. If the user returns
        // via reload, CancelSoftRevokeAsync clears it. If they truly closed,
        // the API cleanup job hard-revokes it after the grace window (~30s).
        // NOTE: does NOT clear the cookie — page is unloading; if it was a
        // reload the cookie must survive so the reloaded page stays logged in.
        public async Task SoftRevokeAsync(HttpContext httpContext, string scheme)
        {
            try
            {
                var authResult = await httpContext.AuthenticateAsync(scheme);
                if (!authResult.Succeeded) return;   // nothing to revoke

                var principal = authResult.Principal!;
                var userType = principal.FindFirst("UserType")?.Value;
                var sessionId = ClaimsHelper.GetSessionId(principal);
                var accessToken = ClaimsHelper.GetAccessToken(principal);
                var cfg = SchemeRegistry.Get(userType);

                if (cfg == null || string.IsNullOrEmpty(sessionId))
                    return;

                var client = _httpClientFactory.CreateClient("ApiClient");
                client.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(accessToken))
                    client.DefaultRequestHeaders.Add("Authorization",
                        $"Bearer {accessToken}");

                var body = JsonConvert.SerializeObject(new
                {
                    MultiSessionId = sessionId,
                    UserType = userType
                });

                //   Short timeout — request rides on fetch keepalive from client
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await client.PostAsync(
                    $"{_apiBaseUrl}{cfg.ApiPrefix}/SoftRevoke",
                    new StringContent(body, Encoding.UTF8, "application/json"),
                    cts.Token);

                _logger.LogDebug("SoftRevoke called. Scheme={Scheme} Sid={Sid}",
                    scheme, sessionId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("SoftRevoke timeout. Scheme={Scheme}", scheme);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SoftRevoke error. Scheme={Scheme}", scheme);
            }
            //  No cookie SignOut — the page may be reloading
        }

        // ── Cancel soft revoke — called on page load (user returned) ──────────
        // Clears PendingRevoke=1 set by SoftRevokeAsync. A truly-closed tab
        // never reaches this (no page loads), so its token stays pending and
        // gets hard-revoked by the cleanup job.
        public async Task CancelSoftRevokeAsync(HttpContext? httpContext, string scheme)
        {
            try
            {
                var authResult = await httpContext.AuthenticateAsync(scheme);
                if (!authResult.Succeeded) return;

                var principal = authResult.Principal!;
                var userType = principal.FindFirst("UserType")?.Value;
                var sessionId = ClaimsHelper.GetSessionId(principal);
                var accessToken = ClaimsHelper.GetAccessToken(principal);
                var cfg = SchemeRegistry.Get(userType);

                if (cfg == null || string.IsNullOrEmpty(sessionId))
                    return;

                var client = _httpClientFactory.CreateClient("ApiClient");
                client.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(accessToken))
                    client.DefaultRequestHeaders.Add("Authorization",
                        $"Bearer {accessToken}");

                var body = JsonConvert.SerializeObject(new
                {
                    MultiSessionId = sessionId,
                    UserType = userType
                });

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await client.PostAsync(
                    $"{_apiBaseUrl}{cfg.ApiPrefix}/CancelSoftRevoke",
                    new StringContent(body, Encoding.UTF8, "application/json"),
                    cts.Token);

                _logger.LogDebug("CancelSoftRevoke called. Scheme={Scheme} Sid={Sid}",
                    scheme, sessionId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("CancelSoftRevoke timeout. Scheme={Scheme}", scheme);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CancelSoftRevoke error. Scheme={Scheme}", scheme);
            }
        }

        #endregion  SoftRevoke
    }
}