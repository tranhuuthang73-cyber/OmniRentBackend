using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;
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
        public IActionResult Index()
        {
            return View();
        }

        // GET: /AdminDashboard/Bookings
        public async Task<IActionResult> Bookings()
        {
            var bookings = await _context.Bookings
                .Include(b => b.Product)
                .Include(b => b.Renter)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
            return View(bookings);
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

        // =============== PRODUCT MANAGEMENT ===============

        // GET: /AdminDashboard/Products
        public async Task<IActionResult> Products()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Owner)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(products);
        }

        // GET: /AdminDashboard/CreateProduct
        public async Task<IActionResult> CreateProduct()
        {
            ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();
            ViewBag.Owners = await _context.Users.Where(u => u.Role == "OWNER").ToListAsync();
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
            [FromForm] string? OwnerId,
            [FromForm] string? Status,
            [FromForm] List<string>? ImageUrls)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ModelState.AddModelError("", "Tên sản phẩm là bắt buộc.");
                ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();
                ViewBag.Owners = await _context.Users.Where(u => u.Role == "OWNER").ToListAsync();
                return View();
            }

            var category = await _context.Categories.FindAsync(CategoryId);
            if (category == null)
            {
                ModelState.AddModelError("", "Danh mục không tồn tại.");
                ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();
                ViewBag.Owners = await _context.Users.Where(u => u.Role == "OWNER").ToListAsync();
                return View();
            }

            // Build images JSON from URL list
            var validUrls = (ImageUrls ?? new List<string>())
                .Where(u => !string.IsNullOrWhiteSpace(u))
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
                OwnerId = string.IsNullOrWhiteSpace(OwnerId) ? currentUserId : OwnerId,
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

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();
            ViewBag.Owners = await _context.Users.Where(u => u.Role == "OWNER").ToListAsync();

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
            [FromForm] string? OwnerId,
            [FromForm] string? Status,
            [FromForm] List<string>? ImageUrls)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            if (string.IsNullOrWhiteSpace(Name))
            {
                ModelState.AddModelError("", "Tên sản phẩm là bắt buộc.");
                ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();
                ViewBag.Owners = await _context.Users.Where(u => u.Role == "OWNER").ToListAsync();
                return View(product);
            }

            var category = await _context.Categories.FindAsync(CategoryId);
            if (category == null)
            {
                ModelState.AddModelError("", "Danh mục không tồn tại.");
                ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();
                ViewBag.Owners = await _context.Users.Where(u => u.Role == "OWNER").ToListAsync();
                return View(product);
            }

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                             ?? User.FindFirst("sub")?.Value 
                             ?? string.Empty;

            var validUrls = (ImageUrls ?? new List<string>())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();

            product.Name = Name;
            product.Description = Description ?? string.Empty;
            product.PricePerDay = PricePerDay ?? 0;
            product.DepositAmount = DepositAmount ?? 0;
            product.CategoryId = CategoryId;
            product.OwnerId = string.IsNullOrWhiteSpace(OwnerId) ? currentUserId : OwnerId;
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
            var product = await _context.Products.FindAsync(id);
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