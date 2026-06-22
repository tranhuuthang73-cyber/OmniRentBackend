using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;
using OmniRentBackend.Services;

namespace OmniRentBackend.Controllers
{
    [ApiController]
    [Route("products")]
    public class ProductController : ControllerBase
    {
        private readonly OmniRentDbContext _context;
        private readonly AiService _aiService;

        public ProductController(OmniRentDbContext context, AiService aiService)
        {
            _context = context;
            _aiService = aiService;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts(
            [FromQuery] string? categorySlug,
            [FromQuery] string? search,
            [FromQuery] double? minPrice,
            [FromQuery] double? maxPrice,
            [FromQuery] string? aiQuery,
            [FromQuery] string? status,
            [FromQuery] string? attributes)
        {
            double parsedMinPrice = minPrice ?? 0;
            double parsedMaxPrice = maxPrice ?? 99999999;
            var parsedAttributes = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(attributes))
            {
                try
                {
                    parsedAttributes = JsonSerializer.Deserialize<Dictionary<string, string>>(attributes) ?? new Dictionary<string, string>();
                }
                catch { }
            }

            // NLP Semantic query matching
            if (!string.IsNullOrEmpty(aiQuery))
            {
                var aiParsed = await _aiService.ParseSemanticQueryAsync(aiQuery);
                if (!string.IsNullOrEmpty(aiParsed.CategorySlug)) categorySlug = aiParsed.CategorySlug;
                if (!string.IsNullOrEmpty(aiParsed.Search)) search = aiParsed.Search;
                if (aiParsed.MinPrice > 0) parsedMinPrice = aiParsed.MinPrice;
                if (aiParsed.MaxPrice < 99999999) parsedMaxPrice = aiParsed.MaxPrice;

                foreach (var attr in aiParsed.Attributes)
                {
                    parsedAttributes[attr.Key] = attr.Value;
                }
            }

            IQueryable<Product> query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Owner)
                .Include(p => p.Reviews)
                .Include(p => p.ProductAttributes)
                    .ThenInclude(pa => pa.Attribute);

            string filterStatus = string.IsNullOrEmpty(status) ? "AVAILABLE" : status;
            query = query.Where(p => p.Status == filterStatus);

            if (!string.IsNullOrEmpty(categorySlug))
            {
                var category = await _context.Categories
                    .Include(c => c.Subcategories)
                    .FirstOrDefaultAsync(c => c.Slug == categorySlug);

                if (category != null)
                {
                    var ids = new List<string> { category.Id };
                    ids.AddRange(category.Subcategories.Select(s => s.Id));
                    query = query.Where(p => ids.Contains(p.CategoryId));
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(s) || 
                                         p.Description.ToLower().Contains(s) ||
                                         (p.Category != null && p.Category.Name.ToLower().Contains(s)) ||
                                         (p.Category != null && p.Category.Parent != null && p.Category.Parent.Name.ToLower().Contains(s)));
            }

            query = query.Where(p => p.PricePerDay >= parsedMinPrice && p.PricePerDay <= parsedMaxPrice);

            var products = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            // Client-side attribute filtering
            if (parsedAttributes.Count > 0)
            {
                products = products.Where(p =>
                {
                    return parsedAttributes.All(kv =>
                    {
                        var match = p.ProductAttributes.FirstOrDefault(pa => pa.Attribute != null && pa.Attribute.Name.ToLower() == kv.Key.ToLower());
                        if (match == null) return false;
                        return match.Value.ToLower().Contains(kv.Value.ToLower());
                    });
                }).ToList();
            }

            // Map DTOs
            var response = products.Select(p =>
            {
                double avgRating = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0;
                List<string> imagesList;
                try
                {
                    imagesList = JsonSerializer.Deserialize<List<string>>(p.ImagesJson) ?? new List<string>();
                }
                catch
                {
                    imagesList = new List<string>();
                }

                return new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.PricePerDay,
                    p.DepositAmount,
                    p.CategoryId,
                    categoryName = p.Category?.Name ?? "Tài sản",
                    p.OwnerId,
                    owner = p.Owner == null ? null : new
                    {
                        p.Owner.Id,
                        p.Owner.FullName,
                        p.Owner.Email,
                        p.Owner.Phone,
                        p.Owner.AvatarUrl,
                        p.Owner.OwnerVerified,
                        p.Owner.Address,
                        p.Owner.PickupAddress
                    },
                    images = imagesList,
                    imagesJson = p.ImagesJson,
                    p.Status,
                    attributes = p.ProductAttributes.Select(pa => new
                    {
                        pa.AttributeId,
                        name = pa.Attribute?.Name,
                        pa.Value
                    }),
                    averageRating = Math.Round(avgRating, 1),
                    totalReviews = p.Reviews.Count,
                    p.CreatedAt,
                    p.UpdatedAt
                };
            });

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(string id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                    .ThenInclude(c => c!.Parent)
                .Include(p => p.Owner)
                .Include(p => p.ProductAttributes)
                    .ThenInclude(pa => pa.Attribute)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.User)
                .Include(p => p.Bookings)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm." });
            }

            double avgRating = product.Reviews.Any() ? product.Reviews.Average(r => r.Rating) : 0;
            List<string> imagesList;
            try
            {
                imagesList = JsonSerializer.Deserialize<List<string>>(product.ImagesJson) ?? new List<string>();
            }
            catch
            {
                imagesList = new List<string>();
            }

            var activeBookings = product.Bookings
                .Where(b => b.Status == "APPROVED" || b.Status == "ONGOING")
                .Select(b => new { b.StartDate, b.EndDate });

            var response = new
            {
                product.Id,
                product.Name,
                product.Description,
                product.PricePerDay,
                product.DepositAmount,
                product.CategoryId,
                category = product.Category == null ? null : new
                {
                    product.Category.Id,
                    product.Category.Name,
                    product.Category.Slug,
                    parent = product.Category.Parent == null ? null : new
                    {
                        product.Category.Parent.Id,
                        product.Category.Parent.Name,
                        product.Category.Parent.Slug
                    }
                },
                categoryName = product.Category?.Name ?? "Tài sản",
                product.OwnerId,
                owner = product.Owner == null ? null : new
                {
                    product.Owner.Id,
                    product.Owner.FullName,
                    product.Owner.Email,
                    product.Owner.Phone,
                    product.Owner.AvatarUrl,
                    product.Owner.OwnerVerified,
                    product.Owner.RenterTrustScore,
                    product.Owner.Address,
                    product.Owner.PickupAddress
                },
                images = imagesList,
                imagesJson = product.ImagesJson,
                product.Status,
                attributes = product.ProductAttributes.Select(pa => new
                {
                    pa.AttributeId,
                    name = pa.Attribute?.Name,
                    pa.Value
                }),
                reviews = product.Reviews.OrderByDescending(r => r.CreatedAt).Select(r => new
                {
                    r.Id,
                    r.Rating,
                    r.Comment,
                    r.CreatedAt,
                    user = r.User == null ? null : new
                    {
                        r.User.Id,
                        r.User.FullName,
                        r.User.AvatarUrl
                    }
                }),
                bookings = activeBookings,
                averageRating = Math.Round(avgRating, 1),
                totalReviews = product.Reviews.Count,
                product.CreatedAt,
                product.UpdatedAt
            };

            return Ok(response);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var category = await _context.Categories.FindAsync(dto.CategoryId);
            if (category == null)
            {
                return NotFound(new { message = "Không tìm thấy danh mục." });
            }

            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                PricePerDay = dto.PricePerDay,
                DepositAmount = dto.DepositAmount,
                CategoryId = dto.CategoryId,
                OwnerId = userId,
                ImagesJson = dto.ImagesJson ?? "[]",
                Status = "PENDING_APPROVAL"
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            if (dto.Attributes != null && dto.Attributes.Count > 0)
            {
                var categoryAttributes = await _context.Attributes
                    .Where(a => a.CategoryId == dto.CategoryId)
                    .ToListAsync();

                foreach (var attr in dto.Attributes)
                {
                    var catAttr = categoryAttributes.FirstOrDefault(a => a.Name == attr.Key);
                    if (catAttr != null)
                    {
                        var prodAttr = new ProductAttribute
                        {
                            ProductId = product.Id,
                            AttributeId = catAttr.Id,
                            Value = attr.Value
                        };
                        _context.ProductAttributes.Add(prodAttr);
                    }
                }
                await _context.SaveChangesAsync();
            }

            return Ok(await GetProductDetailsDtoAsync(product.Id));
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(string id, [FromBody] UpdateProductDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var role = User.FindFirst("role")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm." });
            }

            if (product.OwnerId != userId && role != "ADMIN")
            {
                return Forbid();
            }

            if (dto.Name != null) product.Name = dto.Name;
            if (dto.Description != null) product.Description = dto.Description;
            if (dto.PricePerDay != null) product.PricePerDay = dto.PricePerDay.Value;
            if (dto.DepositAmount != null) product.DepositAmount = dto.DepositAmount.Value;
            if (dto.ImagesJson != null) product.ImagesJson = dto.ImagesJson;
            if (dto.Status != null) product.Status = dto.Status;

            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (dto.Attributes != null)
            {
                // Delete old attributes
                var oldAttrs = await _context.ProductAttributes.Where(pa => pa.ProductId == id).ToListAsync();
                _context.ProductAttributes.RemoveRange(oldAttrs);
                await _context.SaveChangesAsync();

                var categoryAttributes = await _context.Attributes
                    .Where(a => a.CategoryId == product.CategoryId)
                    .ToListAsync();

                foreach (var attr in dto.Attributes)
                {
                    var catAttr = categoryAttributes.FirstOrDefault(a => a.Name == attr.Key);
                    if (catAttr != null)
                    {
                        var prodAttr = new ProductAttribute
                        {
                            ProductId = product.Id,
                            AttributeId = catAttr.Id,
                            Value = attr.Value
                        };
                        _context.ProductAttributes.Add(prodAttr);
                    }
                }
                await _context.SaveChangesAsync();
            }

            return Ok(await GetProductDetailsDtoAsync(id));
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var role = User.FindFirst("role")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm." });
            }

            if (product.OwnerId != userId && role != "ADMIN")
            {
                return Forbid();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [Authorize]
        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApproveProduct(string id)
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "ADMIN") return Forbid();

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm." });
            }

            product.Status = "AVAILABLE";
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpGet("{id}/recommendations")]
        public async Task<IActionResult> GetRecommendations(string id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm." });
            }

            var recommendations = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Owner)
                .Include(p => p.Reviews)
                .Where(p => p.Id != id && p.CategoryId == product.CategoryId && p.Status == "AVAILABLE")
                .Take(4)
                .ToListAsync();

            var response = recommendations.Select(p =>
            {
                double avgRating = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0;
                List<string> imagesList;
                try
                {
                    imagesList = JsonSerializer.Deserialize<List<string>>(p.ImagesJson) ?? new List<string>();
                }
                catch
                {
                    imagesList = new List<string>();
                }

                return new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.PricePerDay,
                    p.DepositAmount,
                    p.CategoryId,
                    categoryName = p.Category?.Name ?? "Tài sản",
                    p.OwnerId,
                    images = imagesList,
                    imagesJson = p.ImagesJson,
                    p.Status,
                    averageRating = Math.Round(avgRating, 1),
                    totalReviews = p.Reviews.Count,
                    p.CreatedAt
                };
            });

            return Ok(response);
        }

        private async Task<object> GetProductDetailsDtoAsync(string id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Owner)
                .Include(p => p.ProductAttributes)
                    .ThenInclude(pa => pa.Attribute)
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return new { };

            double avgRating = product.Reviews.Any() ? product.Reviews.Average(r => r.Rating) : 0;
            List<string> imagesList;
            try
            {
                imagesList = JsonSerializer.Deserialize<List<string>>(product.ImagesJson) ?? new List<string>();
            }
            catch
            {
                imagesList = new List<string>();
            }

            return new
            {
                product.Id,
                product.Name,
                product.Description,
                product.PricePerDay,
                product.DepositAmount,
                product.CategoryId,
                categoryName = product.Category?.Name ?? "Tài sản",
                product.OwnerId,
                owner = product.Owner == null ? null : new
                {
                    product.Owner.Id,
                    product.Owner.FullName,
                    product.Owner.Email,
                    product.Owner.Phone,
                    product.Owner.AvatarUrl,
                    product.Owner.OwnerVerified,
                    product.Owner.Address,
                    product.Owner.PickupAddress
                },
                images = imagesList,
                imagesJson = product.ImagesJson,
                product.Status,
                attributes = product.ProductAttributes.Select(pa => new
                {
                    pa.AttributeId,
                    name = pa.Attribute?.Name,
                    pa.Value
                }),
                averageRating = Math.Round(avgRating, 1),
                totalReviews = product.Reviews.Count,
                product.CreatedAt,
                product.UpdatedAt
            };
        }
    }

    public class CreateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double PricePerDay { get; set; }
        public double DepositAmount { get; set; }
        public string CategoryId { get; set; } = string.Empty;
        public string? ImagesJson { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
    }

    public class UpdateProductDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public double? PricePerDay { get; set; }
        public double? DepositAmount { get; set; }
        public string? ImagesJson { get; set; }
        public string? Status { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
    }
}
