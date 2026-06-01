using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;

namespace OmniRentBackend.Controllers
{
    [ApiController]
    [Route("maintenance")]
    public class MaintenanceController : ControllerBase
    {
        private readonly OmniRentDbContext _context;

        public MaintenanceController(OmniRentDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMaintenanceLogs()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var role = User.FindFirst("role")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            IQueryable<MaintenanceLog> query = _context.MaintenanceLogs
                .Include(m => m.Product);

            if (role != "ADMIN")
            {
                query = query.Where(m => m.OwnerId == userId);
            }

            var logs = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
            return Ok(logs);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateLog([FromBody] CreateLogDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var product = await _context.Products.FindAsync(dto.ProductId);
            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm." });
            }

            if (product.OwnerId != userId)
            {
                return Forbid();
            }

            var log = new MaintenanceLog
            {
                ProductId = dto.ProductId,
                OwnerId = userId,
                IssueDescription = dto.IssueDescription,
                Cost = dto.Cost,
                StartDate = dto.StartDate ?? DateTime.UtcNow,
                Status = "UNDER_REPAIR"
            };

            _context.MaintenanceLogs.Add(log);

            // Update product status
            product.Status = "MAINTENANCE";
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(log);
        }

        [Authorize]
        [HttpPut("{id}/resolve")]
        public async Task<IActionResult> ResolveLog(string id, [FromBody] ResolveLogDto? dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var role = User.FindFirst("role")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var log = await _context.MaintenanceLogs
                .Include(m => m.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (log == null)
            {
                return NotFound(new { message = "Không tìm thấy lịch sử bảo trì." });
            }

            if (log.OwnerId != userId && role != "ADMIN")
            {
                return Forbid();
            }

            log.Status = "RESOLVED";
            log.EndDate = DateTime.UtcNow;
            if (dto != null)
            {
                if (dto.Cost.HasValue) log.Cost = dto.Cost.Value;
                if (!string.IsNullOrEmpty(dto.ResolveNote)) log.IssueDescription += $" | Giải quyết: {dto.ResolveNote}";
            }

            // Restore product status
            if (log.Product != null)
            {
                log.Product.Status = "AVAILABLE";
                log.Product.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(log);
        }

        [Authorize]
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductLogs(string productId)
        {
            var logs = await _context.MaintenanceLogs
                .Where(m => m.ProductId == productId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
            return Ok(logs);
        }
    }

    public class CreateLogDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string IssueDescription { get; set; } = string.Empty;
        public double Cost { get; set; }
        public DateTime? StartDate { get; set; }
    }

    public class ResolveLogDto
    {
        public double? Cost { get; set; }
        public string? ResolveNote { get; set; }
    }
}
