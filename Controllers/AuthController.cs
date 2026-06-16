using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OmniRentBackend.Data;
using OmniRentBackend.Models;

namespace OmniRentBackend.Controllers
{
    [ApiController]
    [Route("api/auth")]  // Đổi thành api/auth để tách biệt với MvcAuthController ([Route("auth")])
    public class AuthController : ControllerBase
    {
        private readonly OmniRentDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(OmniRentDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.PasswordHash) || string.IsNullOrWhiteSpace(dto.FullName))
            {
                return BadRequest(new { message = "Thiếu thông tin đăng ký bắt buộc." });
            }

            var existing = await _context.Users.AnyAsync(u => u.Email == dto.Email);
            if (existing)
            {
                return Conflict(new { message = "Email đã được sử dụng." });
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.PasswordHash);
            var user = new User
            {
                Email = dto.Email,
                PasswordHash = hashedPassword,
                FullName = dto.FullName,
                Phone = dto.Phone,
                Role = string.IsNullOrWhiteSpace(dto.Role) ? "RENTER" : dto.Role,
                RenterTrustScore = 80.0,
                OwnerVerified = false
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(GenerateToken(user));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { message = "Vui lòng điền Email và Mật khẩu." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
            }

            bool isMatch = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isMatch)
            {
                return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
            }

            return Ok(GenerateToken(user));
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Yêu cầu xác thực tài khoản." });
            }

            var user = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FullName,
                    u.Phone,
                    u.Role,
                    u.AvatarUrl,
                    u.RenterTrustScore,
                    u.OwnerVerified,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            return Ok(new { user });
        }

        private object GenerateToken(User user)
        {
            var secret = _configuration["Jwt:Secret"] ?? "OmniRentSuperSecretStartupJwtTokenKey2026!!!";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("role", user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "OmniRent",
                audience: _configuration["Jwt:Audience"] ?? "OmniRentUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return new
            {
                access_token = tokenString,
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    role = user.Role,
                    renterTrustScore = user.RenterTrustScore,
                    ownerVerified = user.OwnerVerified,
                    avatarUrl = user.AvatarUrl
                }
            };
        }
    }

    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterDto
    {
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Role { get; set; }
    }
}
