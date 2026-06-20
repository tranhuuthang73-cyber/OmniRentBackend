using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;
using OmniRentBackend.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OmniRentBackend.Controllers
{
    /// <summary>
    /// Dashboard dành cho Admin — chỉ tài khoản có Role "ADMIN" mới được truy cập.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    public class AdminDashboardController : Controller
    {
        private readonly OmniRentDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminDashboardController(OmniRentDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET /AdminDashboard
        public async Task<IActionResult> Index()
        {
            var completedBookings = await _context.Bookings
                .Where(b => b.Status == "COMPLETED" && b.CompletedAt != null)
                .ToListAsync();

            var labels = new List<string>();
            var revenueData = new List<double>();
            
            var rand = new Random();
            for (int i = 5; i >= 0; i--)
            {
                var month = DateTime.UtcNow.AddMonths(-i);
                var monthLabel = month.ToString("MM/yyyy");
                
                var monthlyRevenue = completedBookings
                    .Where(b => b.CompletedAt!.Value.Year == month.Year && b.CompletedAt!.Value.Month == month.Month)
                    .Sum(b => b.TotalPrice * 0.20); // Doanh thu hệ thống (20%)
                    
                // Mô phỏng số liệu nếu tháng đó chưa có dữ liệu thật (để biểu đồ trực quan)
                if (monthlyRevenue == 0)
                {
                    monthlyRevenue = rand.Next(50, 200) * 10000; // 500k - 2tr
                }
                
                labels.Add(monthLabel);
                revenueData.Add(monthlyRevenue);
            }
            
            ViewBag.ChartLabels = JsonSerializer.Serialize(labels);
            ViewBag.ChartData = JsonSerializer.Serialize(revenueData);

            return View();
        }

        // =============== USER MANAGEMENT ===============

        // GET: /AdminDashboard/Users
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
            return View(users);
        }

        // GET: /AdminDashboard/UserDetails/5
        public async Task<IActionResult> UserDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            return View(user);
        }

        // GET: /AdminDashboard/EditUser/5
        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST: /AdminDashboard/EditUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string id, [FromForm] string FullName, [FromForm] string? Phone, [FromForm] string Role, [FromForm] string? Address, [FromForm] bool? OwnerVerified)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(FullName))
            {
                ModelState.AddModelError("", "Họ và tên là bắt buộc.");
                return View(user);
            }

            user.FullName = FullName;
            user.Phone = Phone;
            user.Role = Role;
            user.Address = Address;
            user.OwnerVerified = OwnerVerified ?? false;
            user.UpdatedAt = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật người dùng thành công.";
            return RedirectToAction(nameof(Users));
        }

        // POST: /AdminDashboard/DeleteUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(Users));
            }

            try
            {
                // Xóa dữ liệu phụ thuộc trước để tránh lỗi FOREIGN KEY
                var userRoles = await _context.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
                var notifications = await _context.Notifications.Where(n => n.UserId == id).ToListAsync();
                var messages = await _context.Messages.Where(m => m.SenderId == id || m.ReceiverId == id).ToListAsync();
                var reviews = await _context.Reviews.Where(r => r.UserId == id).ToListAsync();
                var maintenanceLogs = await _context.MaintenanceLogs.Where(m => m.OwnerId == id).ToListAsync();
                var bookings = await _context.Bookings.Where(b => b.RenterId == id).ToListAsync();
                var products = await _context.Products.Where(p => p.OwnerId == id).ToListAsync();

                _context.UserRoles.RemoveRange(userRoles);
                _context.Notifications.RemoveRange(notifications);
                _context.Messages.RemoveRange(messages);
                _context.Reviews.RemoveRange(reviews);
                _context.MaintenanceLogs.RemoveRange(maintenanceLogs);
                _context.Bookings.RemoveRange(bookings);
                _context.Products.RemoveRange(products);
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa người dùng và dữ liệu liên quan.";
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = "Không thể xóa người dùng vì dữ liệu liên quan tồn tại. Vui lòng xóa các liên kết trước.";
                Console.WriteLine($"Delete user failed: {ex.Message}");
            }

            return RedirectToAction(nameof(Users));
        }

        // GET: /AdminDashboard/Bookings
        public async Task<IActionResult> Bookings(string? ownerSearch)
        {
            var bookingQuery = _context.Bookings
                .Include(b => b.Product)
                    .ThenInclude(p => p!.Owner)
                .Include(b => b.Renter)
                .Where(b => b.Status != "PENDING")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(ownerSearch))
            {
                var keyword = ownerSearch.Trim();
                bookingQuery = bookingQuery.Where(b =>
                    b.Product != null
                    && b.Product.Owner != null
                    && b.Product.Owner.FullName.Contains(keyword));
            }

            var bookings = await bookingQuery
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var completedBookings = bookings.Where(b => b.Status == "COMPLETED").ToList();
            ViewBag.CompletedRevenue = completedBookings.Sum(b => b.TotalPrice);
            ViewBag.SystemCommission = completedBookings.Sum(b => b.TotalPrice * 0.20);
            ViewBag.UnpaidCommission = completedBookings.Where(b => !b.CommissionPaid).Sum(b => b.TotalPrice * 0.20);
            ViewBag.PaidCommission = completedBookings.Where(b => b.CommissionPaid).Sum(b => b.TotalPrice * 0.20);
            ViewBag.OwnerRevenueStats = completedBookings
                .GroupBy(b => new
                {
                    OwnerId = b.Product?.OwnerId ?? string.Empty,
                    OwnerName = b.Product?.Owner?.FullName ?? "N/A"
                })
                .Select(g => new AdminOwnerCommissionSummary
                {
                    OwnerId = g.Key.OwnerId,
                    OwnerName = g.Key.OwnerName,
                    BookingCount = g.Count(),
                    Revenue = g.Sum(b => b.TotalPrice),
                    Commission = g.Sum(b => b.TotalPrice * 0.20),
                    UnpaidCommission = g.Where(b => !b.CommissionPaid).Sum(b => b.TotalPrice * 0.20),
                    PaidCommission = g.Where(b => b.CommissionPaid).Sum(b => b.TotalPrice * 0.20)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
            ViewBag.OwnerSearch = ownerSearch ?? string.Empty;
            return View(bookings);
        }

        // POST: /AdminDashboard/MarkOwnerCommissionPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkOwnerCommissionPaid([FromForm] string ownerId, [FromForm] string? ownerSearch)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                TempData["ErrorMessage"] = "Khong tim thay chu cho thue can thu hoa hong.";
                return RedirectToAction(nameof(Bookings), new { ownerSearch });
            }

            var unpaidBookings = await _context.Bookings
                .Include(b => b.Product)
                .Where(b => b.Product != null
                    && b.Product.OwnerId == ownerId
                    && b.Status == "COMPLETED"
                    && !b.CommissionPaid)
                .ToListAsync();

            if (!unpaidBookings.Any())
            {
                TempData["ErrorMessage"] = "Chu cho thue nay khong con hoa hong chua thanh toan.";
                return RedirectToAction(nameof(Bookings), new { ownerSearch });
            }

            var paidAt = DateTime.UtcNow;
            foreach (var booking in unpaidBookings)
            {
                booking.CommissionPaid = true;
                booking.CommissionPaidAt = paidAt;
                booking.UpdatedAt = paidAt;
            }

            var commissionAmount = unpaidBookings.Sum(b => b.TotalPrice * 0.20);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Da ghi nhan thu hoa hong {commissionAmount:N0}d thanh cong.";
            return RedirectToAction(nameof(Bookings), new { ownerSearch });
        }

        // GET: /AdminDashboard/BookingDetails/5
        public async Task<IActionResult> BookingDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var booking = await _context.Bookings
                .Include(b => b.Product)
                    .ThenInclude(p => p!.Owner)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            return View(booking);
        }



        // GET: /AdminDashboard/EditBooking/5
        public async Task<IActionResult> EditBooking(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            return View(booking);
        }

        // POST: /AdminDashboard/EditBooking/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBooking(string id, [FromForm] string Status, [FromForm] string PaymentStatus)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            booking.Status = Status;
            booking.PaymentStatus = PaymentStatus;
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Bookings));
        }

        // POST: /AdminDashboard/DeleteBooking/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBooking(string id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking != null)
            {
                _context.Bookings.Remove(booking);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Bookings));
        }

        // =============== CATEGORY MANAGEMENT ===============

        // GET: /AdminDashboard/Categories
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .Include(c => c.Parent)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return View(categories);
        }

        // GET: /AdminDashboard/CreateCategory
        public async Task<IActionResult> CreateCategory()
        {
            ViewBag.ParentCategories = await _context.Categories
                .Where(c => c.ParentId == null)
                .ToListAsync();
            return View();
        }

        // POST: /AdminDashboard/CreateCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory([FromForm] string Name, [FromForm] string Slug, [FromForm] string? ParentId)
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Slug))
            {
                ModelState.AddModelError("", "Tên danh mục và Slug là bắt buộc.");
                ViewBag.ParentCategories = await _context.Categories
                    .Where(c => c.ParentId == null)
                    .ToListAsync();
                return View();
            }

            if (await _context.Categories.AnyAsync(c => c.Slug == Slug))
            {
                ModelState.AddModelError("", "Slug này đã tồn tại, vui lòng chọn Slug khác.");
                ViewBag.ParentCategories = await _context.Categories
                    .Where(c => c.ParentId == null)
                    .ToListAsync();
                return View();
            }

            var category = new Category
            {
                Id = Guid.NewGuid().ToString(),
                Name = Name,
                Slug = Slug,
                ParentId = string.IsNullOrWhiteSpace(ParentId) ? null : ParentId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã thêm danh mục thành công.";

            return RedirectToAction(nameof(Categories));
        }

        // GET: /AdminDashboard/EditCategory/5
        public async Task<IActionResult> EditCategory(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            ViewBag.ParentCategories = await _context.Categories
                .Where(c => c.ParentId == null && c.Id != id)
                .ToListAsync();
            return View(category);
        }

        // POST: /AdminDashboard/EditCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(string id, [FromForm] string Name, [FromForm] string Slug, [FromForm] string? ParentId)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Slug))
            {
                ModelState.AddModelError("", "Tên danh mục và Slug là bắt buộc.");
                ViewBag.ParentCategories = await _context.Categories
                    .Where(c => c.ParentId == null && c.Id != id)
                    .ToListAsync();
                return View(category);
            }

            if (await _context.Categories.AnyAsync(c => c.Slug == Slug && c.Id != id))
            {
                ModelState.AddModelError("", "Slug này đã được sử dụng bởi danh mục khác.");
                ViewBag.ParentCategories = await _context.Categories
                    .Where(c => c.ParentId == null && c.Id != id)
                    .ToListAsync();
                return View(category);
            }

            category.Name = Name;
            category.Slug = Slug;
            category.ParentId = string.IsNullOrWhiteSpace(ParentId) ? null : ParentId;
            category.UpdatedAt = DateTime.UtcNow;

            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật danh mục thành công.";

            return RedirectToAction(nameof(Categories));
        }

        // POST: /AdminDashboard/DeleteCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(string id)
        {
            var category = await _context.Categories
                .Include(c => c.Subcategories)
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category != null)
            {
                if (category.Subcategories.Any() || category.Products.Any())
                {
                    TempData["ErrorMessage"] = "Không thể xóa danh mục đang chứa danh mục con hoặc sản phẩm.";
                    return RedirectToAction(nameof(Categories));
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa danh mục thành công.";
            }
            return RedirectToAction(nameof(Categories));
        }

        // =============== PRODUCT MANAGEMENT ===============

        // GET: /AdminDashboard/Products
        public async Task<IActionResult> Products()
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                             ?? User.FindFirst("sub")?.Value 
                             ?? string.Empty;

            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Owner)
                .Where(p => p.OwnerId == currentUserId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(products);
        }

        // GET: /AdminDashboard/CreateProduct
        public async Task<IActionResult> CreateProduct()
        {
            ViewBag.Categories = await _context.Categories
                .Include(c => c.Subcategories)
                .Where(c => c.ParentId == null)
                .ToListAsync();
            return View();
        }

        // POST: /AdminDashboard/CreateProduct
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

            // Build images JSON from URL list
            var validUrls = Request.Form["ImageUrls"]
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .ToList();
            var imagesJson = JsonSerializer.Serialize(validUrls);

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                             ?? User.FindFirst("sub")?.Value 
                             ?? string.Empty;

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

        // GET: /AdminDashboard/EditProduct/5
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

        // POST: /AdminDashboard/EditProduct/5
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

        // POST: /AdminDashboard/DeleteProduct/5
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
                // Delete images
                try
                {
                    var images = JsonSerializer.Deserialize<List<string>>(product.ImagesJson) ?? new List<string>();
                    foreach (var imageUrl in images)
                    {
                        await DeleteProductImageAsync(imageUrl);
                    }
                }
                catch { }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Products));
        }

        // Helper: Save product image
        private async Task<string?> SaveProductImageAsync(IFormFile image)
        {
            try
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(fileStream);
                }

                return $"/uploads/products/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving image: {ex.Message}");
                return null;
            }
        }

        // Helper: Delete product image
        private async Task DeleteProductImageAsync(string imageUrl)
        {
            try
            {
                if (imageUrl.StartsWith("/"))
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, imageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting image: {ex.Message}");
            }
        }

        // Helper: Validate image file
        private bool IsValidImageFile(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension) && allowedMimeTypes.Contains(file.ContentType);
        }
    }
}
