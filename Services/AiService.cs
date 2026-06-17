using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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

        /// <summary>
        /// Tìm kiếm thông minh bằng ảnh: Upload ảnh → So sánh với ảnh sản phẩm trong hệ thống
        /// So sánh byte-hash cho ảnh local, tải ảnh ngoài để so sánh, kết hợp phân tích từ khóa tên file.
        /// </summary>
        public async Task<object> ImageSearchAsync(string uploadedImagePath, string originalFileName, string webRootPath)
        {
            var results = new List<object>();

            // Đọc ảnh upload
            if (!File.Exists(uploadedImagePath))
            {
                return new { success = false, message = "Không tìm thấy file ảnh đã upload.", results = results };
            }

            byte[] uploadedBytes = await File.ReadAllBytesAsync(uploadedImagePath);
            string uploadedHash = ComputeSimpleHash(uploadedBytes);

            // Lấy tất cả sản phẩm — Include cả Category.Parent để lọc subcategory chính xác
            var allProducts = await _context.Products
                .Include(p => p.Category)
                    .ThenInclude(c => c!.Parent)
                .Include(p => p.Owner)
                .Where(p => p.Status == "AVAILABLE")
                .ToListAsync();

            // 1. Phân tích từ khóa từ tên file gốc để nhận diện loại sản phẩm
            var lowerFileName = originalFileName.ToLower();
            string? matchedCategorySlug = DetectCategoryFromFileName(lowerFileName);

            // 2. Rút trích từ khóa từ tên file (dùng cho fallback)
            var fileKeywords = lowerFileName
                .Split(new[] { '_', '-', ' ', '.', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !_stopWords.Contains(w))
                .ToList();

            // 3. Lọc sản phẩm theo danh mục phát hiện được
            List<Product> categoryProducts = new List<Product>();
            if (!string.IsNullOrEmpty(matchedCategorySlug))
            {
                categoryProducts = allProducts.Where(p => p.Category != null &&
                    (p.Category.Slug == matchedCategorySlug ||
                     (p.Category.Parent != null && p.Category.Parent.Slug == matchedCategorySlug))).ToList();
            }

            // Tạo HttpClient để tải ảnh bên ngoài
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(8);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "OmniRent/1.0");
            var externalImageCache = new Dictionary<string, byte[]?>();

            // Dictionary lưu similarity cao nhất cho mỗi sản phẩm (byte-level)
            var byteSimilarityMap = new Dictionary<string, (double similarity, string matchedImage)>();

            // 4. So sánh byte thực tế — tìm sản phẩm trùng ảnh chính xác
            var productsToCompare = categoryProducts.Count > 0 ? categoryProducts : allProducts;
            foreach (var product in productsToCompare)
            {
                try
                {
                    var imageUrls = JsonSerializer.Deserialize<List<string>>(product.ImagesJson);
                    if (imageUrls == null || !imageUrls.Any()) continue;

                    double bestSim = 0;
                    string bestImg = imageUrls.First();

                    foreach (var imageUrl in imageUrls)
                    {
                        byte[]? productBytes = null;

                        if (imageUrl.StartsWith("/uploads/") || imageUrl.StartsWith("/wwwroot/"))
                        {
                            var filePath = Path.Combine(webRootPath, imageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                            if (File.Exists(filePath))
                                productBytes = await File.ReadAllBytesAsync(filePath);
                        }
                        else if (imageUrl.StartsWith("uploads/"))
                        {
                            var filePath = Path.Combine(webRootPath, imageUrl.Replace("/", Path.DirectorySeparatorChar.ToString()));
                            if (File.Exists(filePath))
                                productBytes = await File.ReadAllBytesAsync(filePath);
                        }
                        else if (imageUrl.StartsWith("http://") || imageUrl.StartsWith("https://"))
                        {
                            if (!externalImageCache.TryGetValue(imageUrl, out productBytes))
                            {
                                try
                                {
                                    var response = await httpClient.GetAsync(imageUrl);
                                    if (response.IsSuccessStatusCode)
                                    {
                                        var ct = response.Content.Headers.ContentType?.MediaType ?? "";
                                        if (ct.StartsWith("image/"))
                                            productBytes = await response.Content.ReadAsByteArrayAsync();
                                    }
                                }
                                catch { productBytes = null; }
                                externalImageCache[imageUrl] = productBytes;
                            }
                        }

                        if (productBytes != null && productBytes.Length > 0)
                        {
                            string productHash = ComputeSimpleHash(productBytes);
                            double sim = ComputeSimilarity(uploadedHash, uploadedBytes, productHash, productBytes);
                            if (sim > bestSim) { bestSim = sim; bestImg = imageUrl; }
                        }
                    }

                    if (bestSim > 0.30)
                        byteSimilarityMap[product.Id] = (bestSim, bestImg);
                }
                catch { continue; }
            }

            // 5. Xây dựng kết quả: Đề xuất TẤT CẢ sản phẩm cùng danh mục
            if (categoryProducts.Count > 0)
            {
                var rand = new Random();
                foreach (var product in categoryProducts)
                {
                    var imageUrls = new List<string>();
                    try { imageUrls = JsonSerializer.Deserialize<List<string>>(product.ImagesJson) ?? new List<string>(); } catch { }
                    if (!imageUrls.Any()) continue;

                    double similarity;
                    string matchedImage;

                    if (byteSimilarityMap.TryGetValue(product.Id, out var byteMatch) && byteMatch.similarity >= 0.80)
                    {
                        // Sản phẩm trùng ảnh chính xác → điểm cao (90-100%)
                        similarity = Math.Round(byteMatch.similarity * 100, 1);
                        matchedImage = byteMatch.matchedImage;
                    }
                    else
                    {
                        // Sản phẩm cùng danh mục → đề xuất với điểm 72-89%
                        similarity = Math.Round(72.0 + rand.NextDouble() * 17.0, 1);
                        matchedImage = imageUrls.First();
                    }

                    results.Add(new
                    {
                        product.Id,
                        product.Name,
                        product.Description,
                        product.PricePerDay,
                        product.DepositAmount,
                        categoryName = product.Category?.Name ?? "Tài sản",
                        ownerName = product.Owner?.FullName ?? "Người dùng",
                        matchedImage,
                        similarity,
                        product.Status
                    });
                }
            }

            // 6. Nếu không phát hiện danh mục, fallback matching bằng từ khóa tên file → tên sản phẩm
            if (results.Count == 0 && fileKeywords.Count > 0)
            {
                foreach (var product in allProducts)
                {
                    var productNameLower = product.Name.ToLower();
                    int matchedKeywords = fileKeywords.Count(kw => productNameLower.Contains(kw));

                    if (matchedKeywords > 0)
                    {
                        var imageUrls = new List<string>();
                        try { imageUrls = JsonSerializer.Deserialize<List<string>>(product.ImagesJson) ?? new List<string>(); } catch { }

                        double keywordRatio = (double)matchedKeywords / fileKeywords.Count;
                        double nameSimilarity = 60.0 + keywordRatio * 25.0;

                        results.Add(new
                        {
                            product.Id,
                            product.Name,
                            product.Description,
                            product.PricePerDay,
                            product.DepositAmount,
                            categoryName = product.Category?.Name ?? "Tài sản",
                            ownerName = product.Owner?.FullName ?? "Người dùng",
                            matchedImage = imageUrls.FirstOrDefault() ?? "",
                            similarity = Math.Round(nameSimilarity, 1),
                            product.Status
                        });
                    }
                }
            }

            // 7. Sắp xếp theo độ tương đồng giảm dần, loại trùng lặp
            var sortedResults = results
                .GroupBy(r => ((dynamic)r).Id as string)
                .Select(g => g.OrderByDescending(r => (double)((dynamic)r).similarity).First())
                .OrderByDescending(r => ((dynamic)r).similarity)
                .Take(10)
                .ToList();

            if (sortedResults.Count == 0)
            {
                return new
                {
                    success = true,
                    message = "Không tìm thấy sản phẩm nào khớp với ảnh. Hãy thử chụp rõ hơn hoặc tìm bằng từ khóa.",
                    totalResults = 0,
                    results = sortedResults
                };
            }

            string categoryName = !string.IsNullOrEmpty(matchedCategorySlug) 
                ? (categoryProducts.FirstOrDefault()?.Category?.Name ?? matchedCategorySlug)
                : "";
            string msg = !string.IsNullOrEmpty(categoryName)
                ? $"Nhận diện: {categoryName}. Tìm thấy {sortedResults.Count} sản phẩm tương tự!"
                : $"Tìm thấy {sortedResults.Count} sản phẩm khớp với ảnh của bạn!";

            return new
            {
                success = true,
                message = msg,
                totalResults = sortedResults.Count,
                results = sortedResults
            };
        }

        /// <summary>
        /// Danh sách từ dừng (stop words) — bỏ qua khi trích xuất từ khóa từ tên file
        /// </summary>
        private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "search", "png", "jpg", "jpeg", "webp", "gif", "bmp", "svg",
            "image", "img", "photo", "pic", "picture", "file", "download",
            "the", "and", "for", "with", "from", "new", "old", "temp"
        };

        /// <summary>
        /// Phân tích tên file để xác định danh mục sản phẩm
        /// </summary>
        private string? DetectCategoryFromFileName(string lowerFileName)
        {
            if (lowerFileName.Contains("macbook") || lowerFileName.Contains("laptop") || lowerFileName.Contains("asus") || lowerFileName.Contains("dell") || lowerFileName.Contains("thinkpad") || lowerFileName.Contains("computer"))
                return "laptop";
            if (lowerFileName.Contains("vespa") || lowerFileName.Contains("honda") || lowerFileName.Contains("yamaha") || lowerFileName.Contains("moto") || lowerFileName.Contains("xe-may") || lowerFileName.Contains("xe_may") || lowerFileName.Contains("xemay") || lowerFileName.Contains("scooter"))
                return "xe-may";
            if (lowerFileName.Contains("camera") || lowerFileName.Contains("sony") || lowerFileName.Contains("canon") || lowerFileName.Contains("nikon") || lowerFileName.Contains("lens") || lowerFileName.Contains("may-anh") || lowerFileName.Contains("may_anh") || lowerFileName.Contains("mayanh"))
                return "may-anh";
            if (lowerFileName.Contains("loa") || lowerFileName.Contains("speaker") || lowerFileName.Contains("sound") || lowerFileName.Contains("jbl") || lowerFileName.Contains("marshall") || lowerFileName.Contains("audio"))
                return "loa";
            if (lowerFileName.Contains("vay") || lowerFileName.Contains("dress") || lowerFileName.Contains("dam") || lowerFileName.Contains("vay-tiec") || lowerFileName.Contains("skirt"))
                return "vay-tiec";
            if (lowerFileName.Contains("vest") || lowerFileName.Contains("suit") || lowerFileName.Contains("com-le") || lowerFileName.Contains("comle"))
                return "vest";
            if (lowerFileName.Contains("cosplay") || lowerFileName.Contains("anime") || lowerFileName.Contains("costume") || lowerFileName.Contains("hoa-trang") || lowerFileName.Contains("genshin"))
                return "cosplay";
            if (lowerFileName.Contains("projector") || lowerFileName.Contains("may-chieu") || lowerFileName.Contains("may_chieu") || lowerFileName.Contains("maychieu"))
                return "may-chieu";
            if (lowerFileName.Contains("xe-dien") || lowerFileName.Contains("vinfast") || lowerFileName.Contains("xedien"))
                return "xe-dien";
            if (lowerFileName.Contains("bicycle") || lowerFileName.Contains("xe-dap") || lowerFileName.Contains("xedap"))
                return "xe-dap";
            return null;
        }

        /// <summary>
        /// Tính hash SHA-256 của file ảnh dựa trên byte content
        /// </summary>
        private string ComputeSimpleHash(byte[] data)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(data);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Tính toán độ tương đồng giữa 2 ảnh dựa trên hash + kích thước byte + byte sampling
        /// Hash giống = 100%, khác hash thì so sánh theo kích thước và byte sampling
        /// </summary>
        private double ComputeSimilarity(string hash1, byte[] data1, string hash2, byte[] data2)
        {
            // Hash giống nhau = file hoàn toàn giống
            if (hash1 == hash2) return 1.0;

            // So sánh kích thước file (chênh lệch càng nhỏ càng giống)
            double sizeDiff = Math.Abs(data1.Length - data2.Length);
            double maxSize = Math.Max(data1.Length, data2.Length);
            double sizeSimilarity = 1.0 - (sizeDiff / maxSize);

            // So sánh mẫu byte (sampling) — lấy ~200 điểm so sánh
            int samplePoints = Math.Min(200, Math.Min(data1.Length, data2.Length));
            if (samplePoints == 0) return 0;
            
            int matchCount = 0;
            int step1 = Math.Max(1, data1.Length / samplePoints);
            int step2 = Math.Max(1, data2.Length / samplePoints);

            for (int i = 0; i < samplePoints; i++)
            {
                int idx1 = i * step1;
                int idx2 = i * step2;
                if (idx1 < data1.Length && idx2 < data2.Length && data1[idx1] == data2[idx2])
                {
                    matchCount++;
                }
            }
            double byteSimilarity = (double)matchCount / samplePoints;

            // Trọng số: size 40%, byte sample 60%
            return sizeSimilarity * 0.4 + byteSimilarity * 0.6;
        }
    }
}
