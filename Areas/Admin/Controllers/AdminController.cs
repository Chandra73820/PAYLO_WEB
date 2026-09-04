using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PAYLO_Classes.Associate;
using PAYLO_WEB.Models;
using PAYLO_WEB.Services;
using System.Security.Claims;

namespace PAYLO_WEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminController : Controller
    {
        private readonly CommanHttpServices _httpServices;
        private readonly IAuthService _authService;
        private readonly string? _apiBaseUrl;
        private readonly IConfiguration _configuration;
        private readonly string Allowfiles = ".jpg,.jpeg,.png,.webp,.avif,.pdf";
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(
            ILogger<AdminController> logger,
            IAuthService authService,
            CommanHttpServices commonHttpServices,
            HttpClient httpClient,
            IWebHostEnvironment webHostEnvironment,
            IConfiguration configuration)
        {
            _httpServices = commonHttpServices;
            _authService = authService;
            _configuration = configuration;
            _apiBaseUrl = _configuration.GetValue<string>("ApiUrl");
            _webHostEnvironment = webHostEnvironment;
        }

        // ============================================================
        // GET: /Admin/Login
        // This action displays the Login.cshtml page
        // ============================================================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // ============================================================
        // POST: /Admin/Login
        // This action processes the login form
        // ============================================================
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid Username or Password";
                return View(model);
            }

            try
            {
                // ----------------------------------------------------
                // Login information
                // ----------------------------------------------------
                model.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "::1";
                model.UserAgent = Request.Headers.UserAgent.ToString();
                model.UserType = "Admin";
                model.LoginFrom = "web";
                model.SessionID = "";

                // ----------------------------------------------------
                // Call Admin Login API
                // ----------------------------------------------------
                var data = await _httpServices.PostAsync<LoginResponse>("api/Admin/AdminLogin", model);

                // ----------------------------------------------------
                // Validate API response
                // ----------------------------------------------------
                if (data == null || data.RegID <= 0)
                {
                    TempData["Error"] =
                        data?.Msg ?? "Login failed.";

                    return View(model);
                }

                if (data.Msg is not ("Success" or "POPSUCCESS"))
                {
                    TempData["Error"] =
                        data.Msg ?? "Invalid Username or Password";

                    return View(model);
                }

                // ----------------------------------------------------
                // Token expiry
                // ----------------------------------------------------
                var fallbackMinutes =
                    _configuration.GetValue<int>(
                        "TokenSettings:AccessTokenLifetimeMinutes",
                        120);

                string expiryUtc;

                if (DateTime.TryParse(
                    data.accessTokenExpiry,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var parsedDt))
                {
                    expiryUtc = parsedDt
                        .ToUniversalTime()
                        .ToString("o");
                }
                else
                {
                    expiryUtc = DateTimeOffset.UtcNow
                        .AddMinutes(fallbackMinutes)
                        .ToString("o");
                }

                // ----------------------------------------------------
                // Create Claims
                // ----------------------------------------------------
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, data.Name ?? ""),
                    new Claim("MemberID", data.MemberID ?? ""),
                    new Claim("RegID", data.RegID.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, data.RegID.ToString()),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim("SessionId", data.sessionId ?? ""),
                    new Claim("UserType", "Admin"),
                    new Claim("AccessToken", data.accessToken ?? ""),
                    new Claim("AccessTokenExpiry", expiryUtc),
                    new Claim("RefreshToken", data.RefreshToken ?? "")
                };

                // ----------------------------------------------------
                // Create Identity
                // IMPORTANT: Must match AdminScheme
                // ----------------------------------------------------
                var identity = new ClaimsIdentity(
                    claims,
                    "AdminScheme");

                var principal = new ClaimsPrincipal(identity);

                // ----------------------------------------------------
                // Sign in using AdminScheme
                // IMPORTANT: Must match Program.cs
                // ----------------------------------------------------
                await HttpContext.SignInAsync(
                    "AdminScheme",
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
                    });

                // ----------------------------------------------------
                // Login successful
                // ----------------------------------------------------
                return RedirectToAction("Dashboard", "Admin", new { area = "Admin" });}
            catch (Exception ex)
            {
                TempData["Error"] =
                    $"An error occurred: {ex.Message}";

                return View(model);
            }
        }

        // ============================================================
        // GET: /Admin/Welcome
        // Temporary dashboard/home after successful login
        // ============================================================
        [Authorize(AuthenticationSchemes = "AdminScheme")]
        [HttpGet]
        public IActionResult Welcome()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await _authService.SignOutAsync(HttpContext, "AdminScheme");

            return RedirectToAction("Login", "Admin", new { area = "Admin" });
        }
        public async Task<IActionResult> LogoutGet()
        {
            try
            {
                await _authService.SignOutAsync(HttpContext, "AdminScheme");
            }
            catch { }
            return Ok();   // return Ok not Redirect — fetch doesn't follow redirects
        }
        public IActionResult Dashboard()
        {
            return View();
        }
        //public IActionResult CreditCardPercentage()
        //{
        //    var regId = (User.FindFirst("RegID")?.Value).ToInt();

        //    if (regId > 0)
        //    {
        //        return View();
        //    }

        //    return RedirectToAction("Login");
        //}
        public IActionResult CreditCardPercentage()
        {
            var regIdString = User.FindFirst("RegID")?.Value;

            // Safely convert string to int
            int regId = 0;
            if (!string.IsNullOrEmpty(regIdString))
            {
                int.TryParse(regIdString, out regId);
            }

            if (regId > 0)
            {
                return View();
            }

            return RedirectToAction("Login");
        }

    }
}

