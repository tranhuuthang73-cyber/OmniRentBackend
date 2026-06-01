using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;

namespace OmniRentBackend.Controllers
{
    [ApiController]
    [Route("admin")]
    [Authorize(Roles = "ADMIN")]
    public class AdminController : ControllerBase
    {
        private readonly OmniRentDbContext _context;

        public AdminController(OmniRentDbContext context)
        {
            _context = context;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStats()
        {
            int totalUsers = await _context.Users.CountAsync();
            int totalOwners = await _context.Users.CountAsync(u => u.Role == "OWNER");
            int totalRenters = await _context.Users.CountAsync(u => u.Role == "RENTER");

            int totalProducts = await _context.Products.CountAsync();
            int activeProducts = await _context.Products.CountAsync(p => p.Status == "AVAILABLE");
            int rentedProducts = await _context.Products.CountAsync(p => p.Status == "RENTED");
            int pendingProductsCount = await _context.Products.CountAsync(p => p.Status == "PENDING_APPROVAL");

            var completedBookings = await _context.Bookings
                .Where(b => b.Status == "COMPLETED")
                .Select(b => b.TotalPrice)
                .ToListAsync();
            double totalRevenue = completedBookings.Sum();

            // Bookings count by month (for charts)
            var bookings = await _context.Bookings
                .Select(b => new { b.CreatedAt, b.TotalPrice, b.Status })
                .ToListAsync();

            var monthlyStats = new Dictionary<string, (int count, double revenue)>();
            foreach (var b in bookings)
            {
                // Format: Jan 26, Feb 26, etc.
                string month = b.CreatedAt.ToString("MMM yy", CultureInfo.InvariantCulture);
                if (!monthlyStats.ContainsKey(month))
                {
                    monthlyStats[month] = (0, 0.0);
                }
                var stats = monthlyStats[month];
                int newCount = stats.count + 1;
                double newRev = stats.revenue;
                if (b.Status == "COMPLETED")
                {
                    newRev += b.TotalPrice;
                }
                monthlyStats[month] = (newCount, newRev);
            }

            var monthlyChartData = monthlyStats.Select(kv => new
            {
                name = kv.Key,
                bookings = kv.Value.count,
                revenue = kv.Value.revenue
            }).ToList();

            // Hot categories (most rented)
            var allBookingsWithCategory = await _context.Bookings
                .Include(b => b.Product)
                    .ThenInclude(p => p!.Category)
                .ToListAsync();

            var categoryCounts = new Dictionary<string, int>();
            foreach (var b in allBookingsWithCategory)
            {
                if (b.Product != null && b.Product.Category != null)
                {
                    string catName = b.Product.Category.Name;
                    if (!categoryCounts.ContainsKey(catName))
                    {
                        categoryCounts[catName] = 0;
                    }
                    categoryCounts[catName]++;
                }
            }

            var hotCategories = categoryCounts
                .Select(kv => new { name = kv.Key, value = kv.Value })
                .OrderByDescending(x => x.value)
                .ToList();

            var categoryStats = hotCategories.Select(hc => new
            {
                hc.name,
                revenue = hc.value * 250000.0
            }).ToList();

            int totalBookings = await _context.Bookings.CountAsync();
            int activeBookings = await _context.Bookings.CountAsync(b => b.Status == "APPROVED" || b.Status == "ONGOING");

            // Fraud alerts
            var fraudAlerts = await DetectFraudAsync();

            return Ok(new
            {
                totalRevenue,
                totalUsers,
                ownerCount = totalOwners,
                renterCount = totalRenters,
                totalProducts,
                availableProducts = activeProducts,
                pendingProducts = pendingProductsCount,
                totalBookings,
                activeBookings,
                monthlyRevenue = monthlyChartData,
                categoryStats,
                fraudAlerts
            });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
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
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return Ok(users);
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            if (dto.Role != null) user.Role = dto.Role;
            if (dto.OwnerVerified.HasValue) user.OwnerVerified = dto.OwnerVerified.Value;

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.Role,
                user.OwnerVerified
            });
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        private async Task<List<object>> DetectFraudAsync()
        {
            var alerts = new List<object>();

            // 1. Severe Damage Reports
            var severeReports = await _context.DamageReports
                .Include(r => r.Booking)
                    .ThenInclude(b => b!.Renter)
                .Include(r => r.Booking)
                    .ThenInclude(b => b!.Product)
                .Where(r => r.Severity == "SEVERE")
                .ToListAsync();

            foreach (var rep in severeReports)
            {
                if (rep.Booking != null)
                {
                    alerts.Add(new
                    {
                        id = rep.Id,
                        type = "SEVERE_DAMAGE",
                        title = "Thiệt hại nặng nề phát hiện",
                        details = $"Người thuê {rep.Booking.Renter?.FullName} trả đồ \"{rep.Booking.Product?.Name}\" có thiệt hại nghiêm trọng. Chi phí sửa chữa dự kiến: {rep.RepairEstimate.ToString("N0")}đ. Chi tiết: {rep.Details}",
                        severity = "HIGH",
                        createdAt = rep.CreatedAt
                    });
                }
            }

            // 2. Suspicious Price: Listing price < 20% of the category average price
            var categories = await _context.Categories
                .Include(c => c.Products)
                .ToListAsync();

            foreach (var cat in categories)
            {
                var validProducts = cat.Products.Where(p => p.Status == "AVAILABLE").ToList();
                if (validProducts.Count > 1)
                {
                    double avgPrice = validProducts.Average(p => p.PricePerDay);
                    foreach (var prod in validProducts)
                    {
                        if (prod.PricePerDay < avgPrice * 0.2)
                        {
                            alerts.Add(new
                            {
                                id = prod.Id,
                                type = "SUSPICIOUS_PRICE",
                                title = "Giá rẻ bất thường",
                                details = $"Sản phẩm \"{prod.Name}\" trong danh mục \"{cat.Name}\" có giá thuê {prod.PricePerDay.ToString("N0")}đ/ngày, thấp hơn 20% so với giá trung bình danh mục ({avgPrice.ToString("N0")}đ/ngày).",
                                severity = "MEDIUM",
                                createdAt = prod.CreatedAt
                            });
                        }
                    }
                }
            }

            // 3. Low Trust Score Renter with active ongoing rentals
            var ongoingBookings = await _context.Bookings
                .Include(b => b.Renter)
                .Include(b => b.Product)
                .Where(b => b.Status == "ONGOING")
                .ToListAsync();

            foreach (var b in ongoingBookings)
            {
                if (b.Renter != null && b.Renter.RenterTrustScore < 50)
                {
                    alerts.Add(new
                    {
                        id = b.Id,
                        type = "LOW_TRUST_RENTER",
                        title = "Người thuê có độ tin cậy thấp",
                        details = $"Người dùng {b.Renter.FullName} (Điểm tin cậy: {b.Renter.RenterTrustScore}) đang thực hiện đơn thuê hoạt động cho sản phẩm \"{b.Product?.Name}\".",
                        severity = "HIGH",
                        createdAt = b.CreatedAt
                    });
                }
            }

            // Sort descending by date
            return alerts.OrderByDescending(a => ((dynamic)a).createdAt).Cast<object>().ToList();
        }
    }

    public class UpdateUserDto
    {
        public string? Role { get; set; }
        public bool? OwnerVerified { get; set; }
    }
}
