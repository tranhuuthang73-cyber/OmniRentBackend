using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;

namespace OmniRentBackend.Controllers
{
    /// <summary>
    /// Controller MVC dành riêng cho giao diện web (Razor Views).
    /// Tách biệt với AuthController (API) để tránh xung đột routing.
    /// </summary>
    [Route("auth")]
    public class MvcAuthController : Controller
    {
        private readonly OmniRentDbContext _context;

        public MvcAuthController(OmniRentDbContext context)
        {
            _context = context;
        }

        // GET /auth/login → Hiển thị form đăng nhập
        [HttpGet("login")]
        public IActionResult Login(string? returnUrl = null)
        {
            // Nếu đã đăng nhập rồi thì chuyển thẳng về trang chủ
            if (User.Identity?.IsAuthenticated == true)
            {
                return Redirect(returnUrl ?? "/");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST /auth/login → Xử lý đăng nhập từ form
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            [FromForm] MvcLoginDto dto,
            string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            // Validate input cơ bản
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                ViewBag.Error = "Vui lòng điền đầy đủ Email và Mật khẩu.";
                return View();
            }

            // Tìm user theo email trong database
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLower());

            if (user == null)
            {
                ViewBag.Error = "Email hoặc mật khẩu không đúng.";
                return View();
            }

            // Xác minh mật khẩu bằng BCrypt
            bool isMatch = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isMatch)
            {
                ViewBag.Error = "Email hoặc mật khẩu không đúng.";
                return View();
            }

            // Tạo Claims Identity cho Cookie Authentication
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email,          user.Email),
                new Claim(ClaimTypes.Name,           user.FullName),
                new Claim("role",                    user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Lưu cookie sau khi đóng trình duyệt
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            // Ký cookie và lưu phiên đăng nhập vào trình duyệt
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Điều hướng theo vai trò (Role-based redirect)
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return user.Role.ToUpper() switch
            {
                "ADMIN" => RedirectToAction("Index", "AdminDashboard"),
                "OWNER" => RedirectToAction("Index", "OwnerDashboard"),
                _       => Redirect("/") // RENTER và các role khác → trang chủ
            };
        }

        // GET /auth/logout → Xử lý đăng xuất mượt mà từ thẻ <a> trên Navbar
        // Chấp nhận cả GET và POST để giải quyết triệt để lỗi 405 cho mọi Role
        [HttpGet("logout")]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // Xóa Cookie đăng nhập của người dùng khỏi trình duyệt
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            // Xóa sạch session hoặc cache tạm nếu có
            HttpContext.Response.Cookies.Delete(".AspNetCore.Cookies");
            
            // Đăng xuất xong thì đá người dùng quay về trang chủ
            return Redirect("/");
        }
    }

    /// <summary>
    /// DTO dành riêng cho form đăng nhập MVC (nhận dữ liệu từ HTML form).
    /// </summary>
    public class MvcLoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}