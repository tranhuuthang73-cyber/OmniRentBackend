using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;

namespace OmniRentBackend.Controllers
{
    [ApiController]
    [Route("chat")]
    public class ChatController : ControllerBase
    {
        private readonly OmniRentDbContext _context;

        public ChatController(OmniRentDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet("contacts")]
        public async Task<IActionResult> GetContacts()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Find users who sent messages to us
            var sentToUs = await _context.Messages
                .Where(m => m.ReceiverId == userId)
                .Select(m => m.SenderId)
                .Distinct()
                .ToListAsync();

            // Find users we sent messages to
            var sentByUs = await _context.Messages
                .Where(m => m.SenderId == userId)
                .Select(m => m.ReceiverId)
                .Distinct()
                .ToListAsync();

            var contactIds = sentToUs.Union(sentByUs).Distinct().ToList();

            if (contactIds.Count == 0)
            {
                // Fallback: return 5 random other users
                var fallback = await _context.Users
                    .Where(u => u.Id != userId)
                    .Take(5)
                    .Select(u => new
                    {
                        u.Id,
                        u.FullName,
                        u.Email,
                        u.Role,
                        u.AvatarUrl
                    })
                    .ToListAsync();
                return Ok(fallback);
            }

            var contacts = await _context.Users
                .Where(u => contactIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.AvatarUrl
                })
                .ToListAsync();

            return Ok(contacts);
        }

        [Authorize]
        [HttpGet("history/{otherUserId}")]
        public async Task<IActionResult> GetHistory(string otherUserId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var history = await _context.Messages
                .Where(m => (m.SenderId == userId && m.ReceiverId == otherUserId) || 
                            (m.SenderId == otherUserId && m.ReceiverId == userId))
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return Ok(history);
        }
    }
}
