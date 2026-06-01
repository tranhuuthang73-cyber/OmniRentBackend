using System;
using System.Security.Claims;
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
    [Route("review")]
    public class ReviewController : ControllerBase
    {
        private readonly OmniRentDbContext _context;
        private readonly TrustService _trustService;

        public ReviewController(OmniRentDbContext context, TrustService trustService)
        {
            _context = context;
            _trustService = trustService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var booking = await _context.Bookings.FindAsync(dto.BookingId);
            if (booking == null)
            {
                return NotFound(new { message = "Không tìm thấy đơn thuê." });
            }

            if (booking.RenterId != userId)
            {
                return BadRequest(new { message = "Bạn không thể đánh giá đơn thuê của người khác." });
            }

            var review = new Review
            {
                BookingId = dto.BookingId,
                ProductId = booking.ProductId,
                UserId = userId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Recalculate renter trust score
            await _trustService.RecalculateRenterScoreAsync(booking.RenterId);

            return Ok(review);
        }
    }

    public class CreateReviewDto
    {
        public string BookingId { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
    }
}
