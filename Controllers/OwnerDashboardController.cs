using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OmniRentBackend.Controllers
{
    /// <summary>
    /// Dashboard dành cho Owner — chỉ tài khoản có Role "OWNER" mới được điều hướng vào đây.
    /// </summary>
    [Authorize(Roles = "OWNER")]
    public class OwnerDashboardController : Controller
    {
        private readonly OmniRentDbContext _context;

        public OwnerDashboardController(OmniRentDbContext context)
        {
            _context = context;
        }

        // GET /OwnerDashboard
        public async Task<IActionResult> Index()
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                             ?? User.FindFirst("sub")?.Value 
                             ?? string.Empty;

            // Stats for this owner
            var myProductIds = await _context.Products
                .Where(p => p.OwnerId == currentUserId)
                .Select(p => p.Id)
                .ToListAsync();

            ViewBag.MyProductsCount = myProductIds.Count;

            var pendingBookingsCount = await _context.Bookings
                .Where(b => myProductIds.Contains(b.ProductId) && (b.Status == "PENDING" || b.Status == "WAITING_OWNER_CONFIRM"))
                .CountAsync();
            ViewBag.PendingBookingsCount = pendingBookingsCount;

            var totalRevenue = await _context.Bookings
                .Where(b => myProductIds.Contains(b.ProductId) && b.Status == "COMPLETED")
                .SumAsync(b => b.TotalPrice);
            ViewBag.TotalRevenue = totalRevenue;

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextMonthStart = monthStart.AddMonths(1);
            var monthlyRevenue = await _context.Bookings
                .Where(b => myProductIds.Contains(b.ProductId)
                    && b.Status == "COMPLETED"
                    && b.CompletedAt != null
                    && b.CompletedAt >= monthStart
                    && b.CompletedAt < nextMonthStart)
                .SumAsync(b => b.TotalPrice);
            ViewBag.MonthlyRevenue = monthlyRevenue;
            ViewBag.MonthlyCommission = monthlyRevenue * 0.20;

            ViewBag.RecentBookings = await _context.Bookings
                .Include(b => b.Product)
                .Include(b => b.Renter)
                .Where(b => myProductIds.Contains(b.ProductId))
                .OrderByDescending(b => b.CreatedAt)
                .Take(8)
                .ToListAsync();

            return View();
        }

        // GET: /OwnerDashboard/Products
        public async Task<IActionResult> Products()
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                             ?? User.FindFirst("sub")?.Value 
                             ?? string.Empty;

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.OwnerId == currentUserId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(products);
        }

        // GET: /OwnerDashboard/CreateProduct
        public async Task<IActionResult> CreateProduct()
        {
            ViewBag.Categories = await _context.Categories
                .Include(c => c.Subcategories)
                .Where(c => c.ParentId == null)
                .ToListAsync();
            return View();
        }

        // POST: /OwnerDashboard/CreateProduct
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(
            [FromForm] string Name,
            [FromForm] string? Description,
            [FromForm] double? PricePerDay,
            [FromForm] double? DepositAmount,
            [FromForm] string CategoryId,
            [FromForm] string? Status,
            [FromForm] List<string>? ImageUrls)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ModelState.AddModelError("", "Tên sản phẩm là bắt buộc.");
                ViewBag.Categories = await _context.Categories
                    .Include(c => c.Subcategories)
                    .Where(c => c.ParentId == null)
                    .ToListAsync();
                return View();
            }

            var category = await _context.Categories.FindAsync(CategoryId);
            if (category == null)
            {
                ModelState.AddModelError("", "Danh mục không tồn tại.");
                ViewBag.Categories = await _context.Categories
                    .Include(c => c.Subcategories)
                    .Where(c => c.ParentId == null)
                    .ToListAsync();
                return View();
            }

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                             ?? User.FindFirst("sub")?.Value 
                             ?? string.Empty;

            var validUrls = Request.Form["ImageUrls"]
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .ToList();
            var imagesJson = JsonSerializer.Serialize(validUrls);

            var product = new Product
            {
                Id = Guid.NewGuid().ToString(),
                Name = Name,
                Description = Description ?? string.Empty,
                PricePerDay = PricePerDay ?? 0,
                DepositAmount = DepositAmount ?? 0,
                CategoryId = CategoryId,
                OwnerId = currentUserId,
                ImagesJson = imagesJson,
                Status = Status ?? "AVAILABLE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Products));
        }

        // GET: /OwnerDashboard/EditProduct/5
        public async Task<IActionResult> EditProduct(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                             ?? User.FindFirst("sub")?.Value 
                             ?? string.Empty;

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == currentUserId);

            if (product == null) return NotFound();

            ViewBag.Categories = await _context.Categories
                .Include(c => c.Subcategories)
                .Where(c => c.ParentId == null)
                .ToListAsync();

            return View(product);
        }

        // POST: /OwnerDashboard/EditProduct/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(
            string id,
            [FromForm] string Name,
            [FromForm] string? Description,
            [FromForm] double? PricePerDay,
            [FromForm] double? DepositAmount,
            [FromForm] string CategoryId,
            [FromForm] string? Status,
            [FromForm] List<string>? ImageUrls)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                             ?? User.FindFirst("sub")?.Value 
                             ?? string.Empty;

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == currentUserId);
            if (product == null) return NotFound();

            if (string.IsNullOrWhiteSpace(Name))
            {
                ModelState.AddModelError("", "Tên sản phẩm là bắt buộc.");
                ViewBag.Categories = await _context.Categories
                    .Include(c => c.Subcategories)
                    .Where(c => c.ParentId == null)
                    .ToListAsync();
                return View(product);
            }

            var category = await _context.Categories.FindAsync(CategoryId);
            if (category == null)
            {
                ModelState.AddModelError("", "Danh mục không tồn tại.");
                ViewBag.Categories = await _context.Categories
                    .Include(c => c.Subcategories)
                    .Where(c => c.ParentId == null)
                    .ToListAsync();
                return View(product);
            }

            var validUrls = Request.Form["ImageUrls"]
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .ToList();

            product.Name = Name;
            product.Description = Description ?? string.Empty;
            product.PricePerDay = PricePerDay ?? 0;
            product.DepositAmount = DepositAmount ?? 0;
            product.CategoryId = CategoryId;
            product.Status = Status ?? "AVAILABLE";
            product.ImagesJson = JsonSerializer.Serialize(validUrls);
            product.UpdatedAt = DateTime.UtcNow;

            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Products));
        }

        // POST: /OwnerDashboard/DeleteProduct/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(string id)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                             ?? User.FindFirst("sub")?.Value 
                             ?? string.Empty;

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == currentUserId);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Products));
        }
    }
}
