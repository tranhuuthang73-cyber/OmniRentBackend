using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;
using OmniRentBackend.Models.ViewModels;
using System.Security.Claims;

namespace OmniRentBackend.Controllers
{
    public class AccountController : Controller
    {
        private readonly OmniRentDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher = new();

        public AccountController(OmniRentDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            PasswordVerificationResult result = PasswordVerificationResult.Failed;
            try
            {
                result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            }
            catch (FormatException)
            {
                try
                {
                    // Fallback 1: Kiểm tra mật khẩu băm bằng BCrypt (do DbSeeder tự động tạo)
                    if (BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                    {
                        result = PasswordVerificationResult.Success;
                        
                        // Tự động nâng cấp hash sang chuẩn của ASP.NET Core Identity
                        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
                        await _context.SaveChangesAsync();
                    }
                }
                catch
                {
                    // Fallback 2: Kiểm tra mật khẩu dạng chữ thường (plain-text)
                    if (user.PasswordHash == model.Password)
                    {
                        result = PasswordVerificationResult.Success;
                        
                        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
                        await _context.SaveChangesAsync();
                    }
                }
            }

            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            await SignInUserAsync(user, model.RememberMe);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            // Tự động chuyển hướng Admin vào trang Quản trị
            if (user.Role == "ADMIN")
            {
                return RedirectToAction("Bookings", "AdminDashboard");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError(string.Empty, "Email đã được đăng ký.");
                return View(model);
            }

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = model.Email,
                FullName = model.FullName,
                PasswordHash = _passwordHasher.HashPassword(null!, model.Password),
                Role = "RENTER", // Mặc định
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OwnerVerified = false,
                RenterTrustScore = 80.0
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await SignInUserAsync(user, false);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult LoginWithGoogle(string? returnUrl = null)
        {
            var redirectUrl = Url.Action("GoogleLoginCallback", "Account", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleLoginCallback(string? returnUrl = null)
        {
            // Đọc thông tin user từ cookie tạm (ExternalCookie) do Google middleware tạo ra
            var authenticateResult = await HttpContext.AuthenticateAsync("ExternalCookie");
            if (!authenticateResult.Succeeded)
                return RedirectToAction("Login", new { returnUrl });

            var claims = authenticateResult.Principal?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = email,
                    FullName = name ?? email,
                    PasswordHash = "GOOGLE_AUTH_" + Guid.NewGuid().ToString("N"), // Không có mật khẩu, đăng nhập qua Google
                    Role = "RENTER",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    OwnerVerified = false,
                    RenterTrustScore = 80.0
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            // Xóa cookie tạm của Google, đăng nhập vào session chính (Cookies)
            await HttpContext.SignOutAsync("ExternalCookie");
            await SignInUserAsync(user, false);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        private async Task SignInUserAsync(User user, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role ?? "RENTER")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = isPersistent, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });
        }
    }
}