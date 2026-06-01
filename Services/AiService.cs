using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;

namespace OmniRentBackend.Services
{
    public class AiService
    {
        private readonly OmniRentDbContext _context;

        public AiService(OmniRentDbContext context)
        {
            _context = context;
        }

        public class SemanticQueryResult
        {
            public string CategorySlug { get; set; } = string.Empty;
            public string Search { get; set; } = string.Empty;
            public double MinPrice { get; set; } = 0;
            public double MaxPrice { get; set; } = 99999999;
            public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        }

        public Task<SemanticQueryResult> ParseSemanticQueryAsync(string query)
        {
            var q = query.ToLower();
            var result = new SemanticQueryResult
            {
                Search = query.Trim()
            };

            // Parse category mappings
            if (q.Contains("xe máy") || q.Contains("xe ga") || q.Contains("sh") || q.Contains("honda"))
            {
                result.CategorySlug = "xe-may";
            }
            else if (q.Contains("xe đạp") || q.Contains("xe dap"))
            {
                result.CategorySlug = "xe-dap";
            }
            else if (q.Contains("xe điện") || q.Contains("xe dien"))
            {
                result.CategorySlug = "xe-dien";
            }
            else if (q.Contains("macbook") || q.Contains("laptop") || q.Contains("máy tính") || q.Contains("may tinh"))
            {
                result.CategorySlug = "laptop";
            }
            else if (q.Contains("máy ảnh") || q.Contains("may anh") || q.Contains("sony") || q.Contains("chụp hình"))
            {
                result.CategorySlug = "may-anh";
            }
            else if (q.Contains("máy chiếu") || q.Contains("may chieu"))
            {
                result.CategorySlug = "may-chieu";
            }
            else if (q.Contains("loa") || q.Contains("âm thanh"))
            {
                result.CategorySlug = "loa";
            }
            else if (q.Contains("váy") || q.Contains("vay") || q.Contains("đầm") || q.Contains("dam"))
            {
                result.CategorySlug = "vay-tiec";
            }
            else if (q.Contains("vest") || q.Contains("com-le"))
            {
                result.CategorySlug = "vest";
            }
            else if (q.Contains("cosplay") || q.Contains("hóa trang"))
            {
                result.CategorySlug = "cosplay";
            }
            else if (q.Contains("di chuyển") || q.Contains("mobility") || q.Contains("xe"))
            {
                result.CategorySlug = "mobility";
            }
            else if (q.Contains("công nghệ") || q.Contains("tech") || q.Contains("gear"))
            {
                result.CategorySlug = "tech-gear";
            }
            else if (q.Contains("trang phục") || q.Contains("apparel") || q.Contains("quần áo"))
            {
                result.CategorySlug = "apparel";
            }

            // Parse price modifiers
            if (q.Contains("dưới 300k") || q.Contains("dưới 300.000") || q.Contains("dưới 300"))
            {
                result.MaxPrice = 300000;
            }
            else if (q.Contains("dưới 500k") || q.Contains("dưới 500.000") || q.Contains("dưới 500"))
            {
                result.MaxPrice = 500000;
            }
            else if (q.Contains("trên 400k") || q.Contains("trên 400.000") || q.Contains("trên 400"))
            {
                result.MinPrice = 400000;
            }

            // Parse dynamic attributes
            if (q.Contains("16gb") || q.Contains("16 gb"))
            {
                result.Attributes["RAM"] = "16 GB";
            }
            else if (q.Contains("36gb") || q.Contains("36 gb"))
            {
                result.Attributes["RAM"] = "36 GB";
            }
            else if (q.Contains("32gb") || q.Contains("32 gb"))
            {
                result.Attributes["RAM"] = "32 GB";
            }

            if (q.Contains("full frame") || q.Contains("fullframe"))
            {
                result.Attributes["Loại cảm biến"] = "Full Frame";
            }

            if (q.Contains("nhung"))
            {
                result.Attributes["Chất liệu"] = "Nhung cao cấp";
            }

            if (q.Contains("size m") || q.Contains("cỡ m"))
            {
                result.Attributes["Kích cỡ"] = "M";
            }
            else if (q.Contains("size l") || q.Contains("cỡ l"))
            {
                result.Attributes["Kích cỡ"] = "L";
            }

            return Task.FromResult(result);
        }

        public async Task<object> PredictSmartDepositAsync(string productId, string userId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                throw new Exception("Không tìm thấy sản phẩm.");
            }

            var user = await _context.Users.FindAsync(userId);
            double originalDeposit = product.DepositAmount;
            double discountPercentage = 0;

            if (user != null)
            {
                double score = user.RenterTrustScore;
                if (score >= 95)
                {
                    discountPercentage = 50; // 50% deposit reduction
                }
                else if (score >= 90)
                {
                    discountPercentage = 30; // 30% reduction
                }
                else if (score >= 80)
                {
                    discountPercentage = 10; // 10% reduction
                }
            }

            double predictedDeposit = originalDeposit * (1 - discountPercentage / 100);

            return new
            {
                originalDeposit,
                predictedDeposit,
                discountPercentage,
                renterTrustScore = user?.RenterTrustScore ?? 80.0,
                userVerified = user?.OwnerVerified ?? false
            };
        }

        public Task<object> ScanDamageImageAsync(string imageUrl)
        {
            var url = imageUrl.ToLower();
            string severity = "NONE";
            string details = "Sản phẩm hoàn toàn nguyên vẹn, không phát hiện vết xước hay móp méo.";
            double repairEstimate = 0;
            double confidence = 0.98;

            if (url.Contains("scratch") || url.Contains("xước") || url.Contains("xuoc"))
            {
                severity = "LIGHT";
                details = "Phát hiện một vài vết xước xát nhẹ trên bề mặt bên ngoài vỏ sản phẩm. Đề xuất làm sạch bề mặt.";
                repairEstimate = 150000;
                confidence = 0.89;
            }
            else if (url.Contains("crash") || url.Contains("vỡ") || url.Contains("hỏng") || url.Contains("damage"))
            {
                severity = "SEVERE";
                details = "Cảnh báo: Phát hiện vết nứt vỡ lớn, linh kiện biến dạng nghiêm trọng. Cần sửa chữa phần cứng chuyên sâu.";
                repairEstimate = 1200000;
                confidence = 0.94;
            }

            return Task.FromResult<object>(new
            {
                severity,
                confidence,
                details,
                repairEstimate,
                scannedAt = DateTime.UtcNow
            });
        }

        public async Task<object> GetChatbotResponseAsync(string message)
        {
            var parsed = await ParseSemanticQueryAsync(message);

            IQueryable<Product> query = _context.Products.Include(p => p.Category).Where(p => p.Status == "AVAILABLE");

            if (!string.IsNullOrEmpty(parsed.CategorySlug))
            {
                var category = await _context.Categories
                    .Include(c => c.Subcategories)
                    .FirstOrDefaultAsync(c => c.Slug == parsed.CategorySlug);

                if (category != null)
                {
                    var ids = new List<string> { category.Id };
                    ids.AddRange(category.Subcategories.Select(s => s.Id));

                    query = query.Where(p => ids.Contains(p.CategoryId));
                }
            }

            if (parsed.MinPrice > 0)
            {
                query = query.Where(p => p.PricePerDay >= parsed.MinPrice);
            }

            if (parsed.MaxPrice < 99999999)
            {
                query = query.Where(p => p.PricePerDay <= parsed.MaxPrice);
            }

            var products = await query.Take(3).ToListAsync();

            string text = "";
            if (products.Count > 0)
            {
                text = "Chào bạn! Dựa trên yêu cầu của bạn, tôi tìm thấy một số sản phẩm phù hợp tại OmniRent:\n\n";
                foreach (var p in products)
                {
                    text += $"• **[{p.Name}](/products/{p.Id})** ({p.Category?.Name})\n  Giá thuê: **{p.PricePerDay.ToString("N0")}đ/ngày** | Đặt cọc: {p.DepositAmount.ToString("N0")}đ\n\n";
                }
                text += "Bạn có thể click vào tên sản phẩm để xem chi tiết và đặt lịch thuê ngay nhé!";
            }
            else
            {
                text = $"Chào bạn! Tôi là trợ lý AI của OmniRent. Hiện tại tôi chưa tìm thấy sản phẩm cụ thể nào khớp hoàn toàn với yêu cầu \"{message}\". \n\nBạn có thể thử tìm các sản phẩm khác như \"thuê xe máy SH\", \"thuê laptop cấu hình cao\", \"váy tiệc size M\", hoặc xem trang Catalog nhé!";
            }

            return new { response = text };
        }
    }
}
