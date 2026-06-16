using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using OmniRentBackend.Services;

namespace OmniRentBackend.Controllers
{
    [ApiController]
    [Route("ai")]
    public class AiController : ControllerBase
    {
        private readonly AiService _aiService;
        private readonly IWebHostEnvironment _env;

        public AiController(AiService aiService, IWebHostEnvironment env)
        {
            _aiService = aiService;
            _env = env;
        }

        [HttpGet("deposit-predict")]
        public async Task<IActionResult> PredictDeposit([FromQuery] string productId, [FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { message = "Thiếu productId hoặc userId." });
            }

            try
            {
                var prediction = await _aiService.PredictSmartDepositAsync(productId, userId);
                return Ok(prediction);
            }
            catch (Exception ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("damage-scan")]
        public async Task<IActionResult> ScanDamage([FromBody] DamageScanRequestDto dto)
        {
            if (string.IsNullOrEmpty(dto.ImageUrl))
            {
                return BadRequest(new { message = "Vui lòng cung cấp ImageUrl." });
            }

            var result = await _aiService.ScanDamageImageAsync(dto.ImageUrl);
            return Ok(result);
        }

        [HttpPost("chatbot")]
        public async Task<IActionResult> Chatbot([FromBody] ChatbotRequestDto dto)
        {
            if (string.IsNullOrEmpty(dto.Message))
            {
                return BadRequest(new { message = "Tin nhắn trống." });
            }

            var response = await _aiService.GetChatbotResponseAsync(dto.Message);
            return Ok(response);
        }

        /// <summary>
        /// Tìm kiếm thông minh bằng ảnh — Upload ảnh rồi hệ thống rà soát sản phẩm có ảnh trùng khớp
        /// </summary>
        [HttpPost("image-search")]
        public async Task<IActionResult> ImageSearch(IFormFile? image)
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn một ảnh để tìm kiếm." });
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif", "image/bmp" };
            if (!allowedTypes.Contains(image.ContentType.ToLower()))
            {
                return BadRequest(new { success = false, message = "Chỉ hỗ trợ file ảnh (JPEG, PNG, WebP, GIF)." });
            }

            // Validate file size (max 10MB)
            if (image.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new { success = false, message = "File ảnh quá lớn. Tối đa 10MB." });
            }

            // Lưu file tạm
            var tempDir = Path.Combine(_env.WebRootPath, "uploads", "temp");
            Directory.CreateDirectory(tempDir);
            var fileName = $"search_{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
            var filePath = Path.Combine(tempDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            try
            {
                var result = await _aiService.ImageSearchAsync(filePath, image.FileName, _env.WebRootPath);
                return Ok(result);
            }
            finally
            {
                // Xóa file tạm sau khi xử lý
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
        }
    }

    public class DamageScanRequestDto
    {
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class ChatbotRequestDto
    {
        public string Message { get; set; } = string.Empty;
    }
}
