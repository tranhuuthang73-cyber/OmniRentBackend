using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using OmniRentBackend.Data;
using OmniRentBackend.Models;
using System.IdentityModel.Tokens.Jwt;

namespace OmniRentBackend.Middleware
{
    public class PermissionMiddleware
    {
        private readonly RequestDelegate _next;

        public PermissionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, OmniRentDbContext dbContext)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint == null)
            {
                await _next(context);
                return;
            }

            var requirePermission = endpoint.Metadata.GetMetadata<RequirePermissionAttribute>();
            if (requirePermission == null)
            {
                await _next(context);
                return;
            }

            // Kiểm tra xem người dùng đã xác thực chưa
            var user = context.User;
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Yêu cầu xác thực tài khoản (Token không hợp lệ hoặc thiếu)." });
                return;
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                         ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Không xác định được người dùng từ Token." });
                return;
            }

            // Truy vấn database để kiểm tra quyền hạn của người dùng
            var hasPermission = await dbContext.UserRoles
                .Where(ur => ur.UserId == userId)
                .AnyAsync(ur => ur.Role != null && ur.Role.RolePermissions
                    .Any(rp => rp.Permission != null && rp.Permission.Name == requirePermission.Permission));

            if (!hasPermission)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { 
                    message = $"Bạn không có quyền truy cập chức năng này. Quyền yêu cầu: {requirePermission.Permission}." 
                });
                return;
            }

            await _next(context);
        }
    }
}
