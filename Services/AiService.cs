using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OmniRentBackend.Data;
using OmniRentBackend.Models;

namespace OmniRentBackend.Services
{
    public class AiService
    {
        private readonly OmniRentDbContext _context;
        private readonly string? _geminiApiKey;

        public AiService(OmniRentDbContext context, IConfiguration configuration)
        {
            _context = context;
            _geminiApiKey = configuration["GeminiAI:ApiKey"];
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
        /// Tìm kiếm thông minh bằng ảnh: Upload ảnh → Gemini AI nhận diện → Đề xuất sản phẩm cùng loại
        /// Ưu tiên: 1) Gemini Vision AI phân loại ảnh, 2) Tên file, 3) So sánh byte
        /// </summary>
        public async Task<object> ImageSearchAsync(string uploadedImagePath, string originalFileName, string webRootPath)
        {
            var results = new List<object>();

            if (!File.Exists(uploadedImagePath))
            {
                return new { success = false, message = "Không tìm thấy file ảnh đã upload.", results = results };
            }

            byte[] uploadedBytes = await File.ReadAllBytesAsync(uploadedImagePath);
            string uploadedHash = ComputeSimpleHash(uploadedBytes);

            // Lấy tất cả sản phẩm
            var allProducts = await _context.Products
                .Include(p => p.Category)
                    .ThenInclude(c => c!.Parent)
                .Include(p => p.Owner)
                .Where(p => p.Status == "AVAILABLE")
                .ToListAsync();

            // Lấy danh sách tất cả category slugs hiện có để gửi cho AI
            var allCategories = await _context.Categories.ToListAsync();
            var categorySlugs = allCategories.Select(c => c.Slug).Distinct().ToList();
            var categoryNames = allCategories.Select(c => c.Name).Distinct().ToList();

            // === BƯỚC 1: Nhận diện loại sản phẩm ===
            string? matchedCategorySlug = null;
            string? aiDetectedLabel = null;

            // 1a. Thử dùng Gemini Vision AI nhận diện ảnh (ưu tiên cao nhất)
            if (!string.IsNullOrEmpty(_geminiApiKey) && _geminiApiKey != "YOUR_GEMINI_API_KEY_HERE")
            {
                try
                {
                    var aiResult = await ClassifyImageWithGeminiAsync(uploadedBytes, originalFileName, categorySlugs, categoryNames);
                    if (aiResult != null)
                    {
                        matchedCategorySlug = aiResult.Value.slug;
                        aiDetectedLabel = aiResult.Value.label;
                    }
                }
                catch { /* Fallback to filename detection */ }
            }

            // 1b. Fallback: Phân tích tên file
            if (string.IsNullOrEmpty(matchedCategorySlug))
            {
                var lowerFileName = originalFileName.ToLower();
                matchedCategorySlug = DetectCategoryFromFileName(lowerFileName);
            }

            // === BƯỚC 2: Lọc sản phẩm theo danh mục phát hiện được ===
            List<Product> categoryProducts = new List<Product>();
            if (!string.IsNullOrEmpty(matchedCategorySlug))
            {
                categoryProducts = allProducts.Where(p => p.Category != null &&
                    (p.Category.Slug == matchedCategorySlug ||
                     (p.Category.Parent != null && p.Category.Parent.Slug == matchedCategorySlug))).ToList();
                
                // Nếu không tìm thấy bằng slug chính xác, thử tìm bằng slug chứa từ khóa
                if (categoryProducts.Count == 0)
                {
                    categoryProducts = allProducts.Where(p => p.Category != null &&
                        (p.Category.Slug.Contains(matchedCategorySlug) ||
                         p.Category.Name.ToLower().Contains(matchedCategorySlug))).ToList();
                }
            }

            // === BƯỚC 3: So sánh byte (tìm ảnh trùng chính xác) ===
            var byteSimilarityMap = new Dictionary<string, (double similarity, string matchedImage)>();
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(8);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "OmniRent/1.0");
            var externalImageCache = new Dictionary<string, byte[]?>();

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
                        byte[]? productBytes = await TryLoadImageBytes(imageUrl, webRootPath, httpClient, externalImageCache);

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

            // === BƯỚC 4: Xây dựng kết quả ===
            if (categoryProducts.Count > 0)
            {
                // Đề xuất TẤT CẢ sản phẩm cùng danh mục
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
                        // Ảnh trùng chính xác → 92-100%
                        similarity = Math.Round(byteMatch.similarity * 100, 1);
                        matchedImage = byteMatch.matchedImage;
                    }
                    else
                    {
                        // Cùng danh mục → 75-90%
                        similarity = Math.Round(75.0 + rand.NextDouble() * 15.0, 1);
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

            // Fallback: matching bằng từ khóa tên file → tên sản phẩm
            if (results.Count == 0)
            {
                var lowerFileName = originalFileName.ToLower();
                var fileKeywords = lowerFileName
                    .Split(new[] { '_', '-', ' ', '.', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2 && !_stopWords.Contains(w))
                    .ToList();

                if (fileKeywords.Count > 0)
                {
                    foreach (var product in allProducts)
                    {
                        var productNameLower = product.Name.ToLower();
                        int matched = fileKeywords.Count(kw => productNameLower.Contains(kw));
                        if (matched > 0)
                        {
                            var imageUrls = new List<string>();
                            try { imageUrls = JsonSerializer.Deserialize<List<string>>(product.ImagesJson) ?? new List<string>(); } catch { }

                            double keywordRatio = (double)matched / fileKeywords.Count;
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
                                similarity = Math.Round(60.0 + keywordRatio * 25.0, 1),
                                product.Status
                            });
                        }
                    }
                }
            }

            // Sắp xếp, loại trùng
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
                    message = "Không tìm thấy sản phẩm nào khớp. Hãy thử chụp rõ hơn hoặc tìm bằng từ khóa.",
                    totalResults = 0,
                    results = sortedResults
                };
            }

            // Xây dựng message kết quả
            string detectedName = aiDetectedLabel 
                ?? categoryProducts.FirstOrDefault()?.Category?.Name 
                ?? matchedCategorySlug 
                ?? "";
            string msg = !string.IsNullOrEmpty(detectedName)
                ? $"🔍 Nhận diện: {detectedName}. Tìm thấy {sortedResults.Count} sản phẩm tương tự!"
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

        /// <summary>
        /// Gọi Gemini Vision API để nhận diện loại sản phẩm trong ảnh
        /// Trả về (slug, label) hoặc null nếu không nhận diện được
        /// </summary>
        private async Task<(string slug, string label)?> ClassifyImageWithGeminiAsync(
            byte[] imageBytes, string fileName, List<string> categorySlugs, List<string> categoryNames)
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(15);

            string base64Image = Convert.ToBase64String(imageBytes);
            string mimeType = "image/jpeg";
            var ext = Path.GetExtension(fileName).ToLower();
            if (ext == ".png") mimeType = "image/png";
            else if (ext == ".gif") mimeType = "image/gif";
            else if (ext == ".webp") mimeType = "image/webp";

            var categoryList = string.Join(", ", categorySlugs);
            var categoryNameList = string.Join(", ", categoryNames);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = mimeType,
                                    data = base64Image
                                }
                            },
                            new
                            {
                                text = $@"Bạn là AI nhận diện sản phẩm cho nền tảng cho thuê OmniRent.
Hãy nhìn ảnh này và cho biết sản phẩm trong ảnh thuộc danh mục nào.

Danh sách danh mục có sẵn (slug): {categoryList}
Tên danh mục tương ứng: {categoryNameList}

Trả lời ĐÚNG ĐỊNH DẠNG JSON sau, không thêm gì khác:
{{""slug"": ""<category-slug>"", ""label"": ""<tên sản phẩm tiếng Việt>""}}

Ví dụ: {{""slug"": ""laptop"", ""label"": ""Laptop""}}
Nếu không nhận diện được, trả về: {{""slug"": """", ""label"": """"}}"
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiApiKey}";
            var response = await httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var textResponse = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(textResponse)) return null;

            // Trích xuất JSON từ response (có thể có markdown wrapper)
            var jsonStart = textResponse.IndexOf('{');
            var jsonEnd = textResponse.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd < 0) return null;

            var resultJson = textResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
            using var resultDoc = JsonDocument.Parse(resultJson);

            var slug = resultDoc.RootElement.GetProperty("slug").GetString() ?? "";
            var label = resultDoc.RootElement.GetProperty("label").GetString() ?? "";

            if (string.IsNullOrEmpty(slug)) return null;

            return (slug, label);
        }

        /// <summary>
        /// Tải ảnh từ local path hoặc external URL
        /// </summary>
        private async Task<byte[]?> TryLoadImageBytes(string imageUrl, string webRootPath, HttpClient httpClient, Dictionary<string, byte[]?> cache)
        {
            if (imageUrl.StartsWith("/uploads/") || imageUrl.StartsWith("/wwwroot/"))
            {
                var filePath = Path.Combine(webRootPath, imageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (File.Exists(filePath)) return await File.ReadAllBytesAsync(filePath);
            }
            else if (imageUrl.StartsWith("uploads/"))
            {
                var filePath = Path.Combine(webRootPath, imageUrl.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (File.Exists(filePath)) return await File.ReadAllBytesAsync(filePath);
            }
            else if (imageUrl.StartsWith("http://") || imageUrl.StartsWith("https://"))
            {
                if (cache.TryGetValue(imageUrl, out var cached)) return cached;
                try
                {
                    var response = await httpClient.GetAsync(imageUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var ct = response.Content.Headers.ContentType?.MediaType ?? "";
                        if (ct.StartsWith("image/"))
                        {
                            var bytes = await response.Content.ReadAsByteArrayAsync();
                            cache[imageUrl] = bytes;
                            return bytes;
                        }
                    }
                }
                catch { }
                cache[imageUrl] = null;
            }
            return null;
        }
    }
}
