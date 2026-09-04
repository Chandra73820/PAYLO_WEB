using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using PAYLO_WEB.Auth;
using PAYLO_WEB.Helpers;

namespace PAYLO_WEB.Services
{
    public class CommanHttpServices
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CommanHttpServices> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public CommanHttpServices(HttpClient httpClient,
                                   ILogger<CommanHttpServices> logger,
                                   IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        // ── GET ───────────────────────────────────────────────────────────────
        public async Task<T?> GetAsync<T>(string endpoint)
        {
            try
            {
                var url = _httpClient.BaseAddress + endpoint;
                var request = await BuildRequestAsync(HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GET {Endpoint} returned {Status}",
                        endpoint, response.StatusCode);
                    return default;   // ✅ return null/default — do not throw
                }

                return await DeserializeAsync<T>(response, url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET failed: {Endpoint}", endpoint);
                return default;       // ✅ return null/default — do not throw
            }
        }

        // ── GET raw JSON ──────────────────────────────────────────────────────
        public async Task<string?> GetJsonStringAsync(string endpoint)
        {
            try
            {
                var url = _httpClient.BaseAddress + endpoint;
                var request = await BuildRequestAsync(HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GET JSON {Endpoint} returned {Status}",
                        endpoint, response.StatusCode);
                    return null;
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET JSON failed: {Endpoint}", endpoint);
                return null;
            }
        }

        // ── POST ──────────────────────────────────────────────────────────────
        public async Task<T?> PostAsync<T>(string endpoint, object? data)
        {
            try
            {
                var url = _httpClient.BaseAddress + endpoint;
                var request = await BuildRequestAsync(HttpMethod.Post, url, data);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "POST {Endpoint} returned {Status}: {Error}",
                        endpoint, (int)response.StatusCode, error);
                    return default;
                }

                return await DeserializeAsync<T>(response, url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST failed: {Endpoint}", endpoint);
                return default;       // return null/default — do not throw
            }
        }

        // ── POST with custom headers ──────────────────────────────────────────
        public async Task<T?> PostWithHeadersAsync<T>(string endpoint,
            object? data, Dictionary<string, string> extraHeaders)
        {
            try
            {
                var url = _httpClient.BaseAddress + endpoint;
                var request = await BuildRequestAsync(HttpMethod.Post, url,
                                   data, extraHeaders);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "POST with headers {Endpoint} returned {Status}: {Error}",
                        endpoint, response.StatusCode, error);
                    return default;
                }

                return await DeserializeAsync<T>(response, url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST with headers failed: {Endpoint}", endpoint);
                return default;
            }
        }

        // ── POST no response body ─────────────────────────────────────────────
        public async Task<bool> PostAsync(string endpoint, object? data = null)
        {
            try
            {
                var url = _httpClient.BaseAddress + endpoint;
                var request = await BuildRequestAsync(HttpMethod.Post, url, data);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("POST (no response) {Endpoint} returned {Status}",
                        endpoint, response.StatusCode);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST (no response) failed: {Endpoint}", endpoint);
                return false;
            }
        }

        // ── Build request — async, handles Controller + ViewComponent ─────────
        private async Task<HttpRequestMessage> BuildRequestAsync(
            HttpMethod method, string? url,
            object? body = null,
            Dictionary<string, string>? extraHeaders = null)
        {
            var request = new HttpRequestMessage(method, url);
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext != null)
            {
                var area = httpContext.GetRouteValue("area")?.ToString() ?? "";
                var cfg = SchemeRegistry.GetByArea(area);

                ClaimsPrincipal? resolvedUser = null;

                // Case 1 — default scheme user authenticated (Controller)
                if (httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    resolvedUser = ResolveCorrectUser(httpContext.User, cfg);
                }
                // Case 2 — default not authenticated, try area scheme (ViewComponent)
                else if (cfg != null)
                {
                    var result = await httpContext.AuthenticateAsync(cfg.SchemeName);
                    if (result.Succeeded)
                        resolvedUser = result.Principal;
                }
                // Case 3 — no area, try all schemes
                else
                {
                    foreach (var name in SchemeRegistry.AllSchemeNames)
                    {
                        var result = await httpContext.AuthenticateAsync(name);
                        if (result.Succeeded)
                        {
                            resolvedUser = result.Principal;
                            break;
                        }
                    }
                }

                // ✅ Only add headers if correct user found
                // null = not authenticated → send no token
                // API's [AllowAnonymous] endpoints work without token ✅
                // API's [Authorize] endpoints return 401 → caller handles ✅
                if (resolvedUser != null)
                {
                    var accessToken = ClaimsHelper.GetAccessToken(resolvedUser);
                    var sessionId = ClaimsHelper.GetSessionId(resolvedUser);

                    if (!string.IsNullOrEmpty(accessToken))
                        request.Headers.TryAddWithoutValidation(
                            "Authorization", $"Bearer {accessToken}");

                    if (!string.IsNullOrEmpty(sessionId))
                        request.Headers.TryAddWithoutValidation(
                            "X-Session-Id", sessionId);
                }
            }

            // Extra headers override defaults
            if (extraHeaders != null)
                foreach (var kv in extraHeaders)
                    request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);

            if (body != null)
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body, _jsonOptions),
                    Encoding.UTF8, "application/json");

            return request;
        }

        // ── Resolve correct user for area ─────────────────────────────────────
        private static ClaimsPrincipal? ResolveCorrectUser(
            ClaimsPrincipal currentUser, SchemeConfig? cfg)
        {
            if (cfg == null) return currentUser;

            var currentUserType = currentUser.FindFirst("UserType")?.Value;
            if (SchemeRegistry.Get(currentUserType)?.SchemeName == cfg.SchemeName)
                return currentUser;

            foreach (var identity in currentUser.Identities)
            {
                var ut = identity.FindFirst("UserType")?.Value;
                if (SchemeRegistry.Get(ut)?.SchemeName == cfg.SchemeName)
                    return new ClaimsPrincipal(identity);
            }

            return null;
        }

        // ── Deserialize response ──────────────────────────────────────────────
        private static async Task<T?> DeserializeAsync<T>(
            HttpResponseMessage response, string? url)
        {
            try
            {
                var json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json)) return default;
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Deserialization failed for: {url}", ex);
            }
        }
    }
}