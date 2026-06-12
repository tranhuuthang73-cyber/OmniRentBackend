using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
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
            var categories = await _context.Categories.ToListAsync();
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Owner)
                .Where(p => p.Status == "AVAILABLE");

            if (!string.IsNullOrEmpty(categoryId))
            {
                query = query.Where(p => p.CategoryId == categoryId);
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
        public async Task<IActionResult> MyBookings()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var bookings = await _context.Bookings
                .Include(b => b.Product)
                    .ThenInclude(p => p!.Owner)
                .Where(b => b.RenterId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }
    }
}
