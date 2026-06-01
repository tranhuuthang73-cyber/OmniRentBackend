using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OmniRentBackend.Services;

namespace OmniRentBackend.Controllers
{
    [ApiController]
    [Route("ai")]
    public class AiController : ControllerBase
    {
        private readonly AiService _aiService;

        public AiController(AiService aiService)
        {
            _aiService = aiService;
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
