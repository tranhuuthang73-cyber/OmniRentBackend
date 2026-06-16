using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniRentBackend.Models;
using System.Security.Claims;

namespace OmniRentBackend.Controllers
{
    [ApiController]
    [Route("test-permission")]
    [Authorize] // Yêu cầu token hợp lệ trước khi vào các route con
    public class TestPermissionController : ControllerBase
    {
        [HttpGet("admin-only")]
        [RequirePermission("MANAGE_USERS")]
        public IActionResult GetAdminOnlyData()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            return Ok(new
            {
                message = "Truy cập thành công! API này chỉ dành cho tài khoản có quyền MANAGE_USERS (Admin).",
                userEmail = email
            });
        }

        [HttpGet("owner-or-admin")]
        [RequirePermission("CREATE_PRODUCT")]
        public IActionResult GetOwnerOrAdminData()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            return Ok(new
            {
                message = "Truy cập thành công! API này yêu cầu quyền CREATE_PRODUCT (Owner hoặc Admin).",
                userEmail = email
            });
        }

        [HttpGet("renter-view")]
        [RequirePermission("BOOK_PRODUCT")]
        public IActionResult GetRenterViewData()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            return Ok(new
            {
                message = "Truy cập thành công! API này yêu cầu quyền BOOK_PRODUCT (Renter hoặc Admin).",
                userEmail = email
            });
        }

        [HttpGet("public-auth")]
        public IActionResult GetPublicAuthData()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            return Ok(new
            {
                message = "Truy cập thành công! API này chỉ yêu cầu Token hợp lệ, không kiểm tra quyền cụ thể.",
                userEmail = email
            });
        }
    }
}
