using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;

namespace OmniRentBackend.Controllers
{
    [ApiController]
    [Route("categories")]
    public class CategoryController : ControllerBase
    {
        private readonly OmniRentDbContext _context;

        public CategoryController(OmniRentDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Include(c => c.Subcategories)
                .ToListAsync();
            return Ok(categories);
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> GetCategoryBySlug(string slug)
        {
            var category = await _context.Categories
                .Include(c => c.Subcategories)
                .FirstOrDefaultAsync(c => c.Slug == slug);

            if (category == null)
            {
                return NotFound(new { message = "Không tìm thấy danh mục." });
            }

            return Ok(category);
        }

        [HttpGet("{id}/attributes")]
        public async Task<IActionResult> GetCategoryAttributes(string id)
        {
            var attributes = await _context.Attributes
                .Where(a => a.CategoryId == id)
                .ToListAsync();
            return Ok(attributes);
        }
    }
}
