using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;
using OmniRentBackend.Models.ViewModels;

namespace OmniRentBackend.Controllers
{
    /// <summary>
    /// Trang chủ chung — hiển thị cho tất cả người dùng.
    /// </summary>
    public class HomeController : Controller
    {
        private readonly OmniRentDbContext _context;

        public HomeController(OmniRentDbContext context)
        {
            _context = context;
        }

        // GET /  hoặc  /Home/Index
        public async Task<IActionResult> Index(string? categoryId)
        {
            var categories = await _context.Categories
                .Include(c => c.Subcategories)
                .Where(c => c.ParentId == null)
                .ToListAsync();

            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Owner)
                .Where(p => p.Status == "AVAILABLE");

            if (!string.IsNullOrEmpty(categoryId))
            {
                var category = await _context.Categories
                    .Include(c => c.Subcategories)
                    .FirstOrDefaultAsync(c => c.Id == categoryId);

                var ids = new List<string> { categoryId };
                if (category != null && category.Subcategories != null)
                {
                    ids.AddRange(category.Subcategories.Select(s => s.Id));
                }

                query = query.Where(p => ids.Contains(p.CategoryId));
                ViewBag.SelectedCategoryId = categoryId;
            }

            var featuredProducts = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            var viewModel = new HomeViewModel
            {
                Categories = categories,
                FeaturedProducts = featuredProducts
            };

            return View(viewModel);
        }

        // GET /Home/ProductDetails/{id}
        public async Task<IActionResult> ProductDetails(string id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Owner)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.User)
                .Include(p => p.ProductAttributes)
                    .ThenInclude(pa => pa.Attribute)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET /Home/Payment/{id}
        public async Task<IActionResult> Payment(string id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Product)
                    .ThenInclude(p => p!.Owner)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound();
            }

            // Bảo mật: Chỉ người đặt đơn mới có quyền xem trang thanh toán này
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (booking.RenterId != userId)
            {
                return Forbid();
            }

            return View(booking);
        }

        // GET /Home/Checkout/{id}
        public async Task<IActionResult> Checkout(string id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Owner)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET /Home/Cart
        public IActionResult Cart()
        {
            return View();
        }

        // GET /Home/MyBookings
        [Authorize]
        public IActionResult MyBookings()
        {
            return RedirectToAction("Profile", "Account");
        }

        // GET /Home/CreateTestCompletedBooking
        public async Task<IActionResult> CreateTestCompletedBooking()
        {
            var renter = await _context.Users.FirstOrDefaultAsync(u => u.Email == "renter@omnirent.com");
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Status == "AVAILABLE");
            if (renter == null || product == null)
            {
                return Content("Error: renter or product not found. Ensure DB is seeded.");
            }

            // Clean up any existing bookings for this product-renter combo to prevent clutter
            var oldBookings = await _context.Bookings.Where(b => b.RenterId == renter.Id && b.ProductId == product.Id).ToListAsync();
            _context.Bookings.RemoveRange(oldBookings);

            var booking = new Booking
            {
                ProductId = product.Id,
                RenterId = renter.Id,
                StartDate = DateTime.UtcNow.AddDays(-3),
                EndDate = DateTime.UtcNow.AddDays(-1),
                TotalPrice = product.PricePerDay * 2,
                Status = "COMPLETED",
                PaymentStatus = "PAID",
                RentalAddress = "123 Test Street, Ho Chi Minh City"
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return Content($"Success: Created completed booking {booking.Id} for product {product.Name} (Renter: {renter.FullName})");
        }
    }
}
