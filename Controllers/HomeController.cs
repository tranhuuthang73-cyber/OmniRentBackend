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
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories.Take(8).ToListAsync();
            var featuredProducts = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Owner)
                .Where(p => p.Status == "AVAILABLE")
                .OrderByDescending(p => p.CreatedAt)
                .Take(8)
                .ToListAsync();

            var viewModel = new HomeViewModel
            {
                Categories = categories,
                FeaturedProducts = featuredProducts
            };

            return View(viewModel);
        }
    }
}
