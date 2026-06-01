using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;

namespace OmniRentBackend.Services
{
    public class TrustService
    {
        private readonly OmniRentDbContext _context;

        public TrustService(OmniRentDbContext context)
        {
            _context = context;
        }

        public async Task<object> RecalculateRenterScoreAsync(string userId)
        {
            var user = await _context.Users
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Reviews)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                throw new Exception("Không tìm thấy người dùng.");
            }

            var completedBookings = user.Bookings.Where(b => b.Status == "COMPLETED").ToList();
            var cancelledBookings = user.Bookings.Where(b => b.Status == "CANCELLED").ToList();

            int positiveRentals = completedBookings.Count;
            int negativeIncidents = cancelledBookings.Count > 2 ? (int)Math.Floor((double)cancelledBookings.Count / 2) : 0;

            double score = 80.0;
            score += positiveRentals * 2.5;
            score -= negativeIncidents * 10.0;

            var ratings = completedBookings.SelectMany(b => b.Reviews).Select(r => r.Rating).ToList();
            if (ratings.Count > 0)
            {
                double avgRating = ratings.Average();
                if (avgRating >= 4.5) score += 5;
                else if (avgRating < 3.0) score -= 10;
            }

            score = Math.Max(0.0, Math.Min(100.0, score));

            user.RenterTrustScore = score;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new
            {
                userId,
                fullName = user.FullName,
                renterTrustScore = score,
                positiveRentals,
                negativeIncidents,
                updatedAt = DateTime.UtcNow
            };
        }

        public async Task<object> VerifyOwnerAsync(string userId, bool verified)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new Exception("Không tìm thấy người dùng.");
            }

            user.OwnerVerified = verified;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new
            {
                user.Id,
                fullName = user.FullName,
                user.Role,
                ownerVerified = user.OwnerVerified
            };
        }
    }
}
