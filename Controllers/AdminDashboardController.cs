using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OmniRentBackend.Controllers
{
    /// <summary>
    /// Dashboard dành cho Admin — chỉ tài khoản có Role "ADMIN" mới được truy cập.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    public class AdminDashboardController : Controller
    {
        private readonly OmniRentDbContext _context;

        public AdminDashboardController(OmniRentDbContext context)
        {
            _context = context;
        }

        // GET /AdminDashboard
        public IActionResult Index()
        {
            return View();
        }

        // GET: /AdminDashboard/Bookings
        public async Task<IActionResult> Bookings()
        {
            var bookings = await _context.Bookings
                .Include(b => b.Product)
                .Include(b => b.Renter)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
            return View(bookings);
        }

        // GET: /AdminDashboard/BookingDetails/5
        public async Task<IActionResult> BookingDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var booking = await _context.Bookings
                .Include(b => b.Product)
                    .ThenInclude(p => p!.Owner)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            return View(booking);
        }

        // GET: /AdminDashboard/CreateBooking
        public async Task<IActionResult> CreateBooking()
        {
            ViewBag.Products = await _context.Products.Where(p => p.Status == "AVAILABLE").ToListAsync();
            ViewBag.Renters = await _context.Users.ToListAsync(); // Lấy tất cả user
            return View();
        }

        // POST: /AdminDashboard/CreateBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBooking([FromForm] string ProductId, [FromForm] string RenterId, [FromForm] DateTime StartDate, [FromForm] DateTime EndDate)
        {
            var product = await _context.Products.FindAsync(ProductId);
            if (product == null || string.IsNullOrEmpty(RenterId))
            {
                ModelState.AddModelError("", "Dữ liệu không hợp lệ.");
                ViewBag.Products = await _context.Products.Where(p => p.Status == "AVAILABLE").ToListAsync();
                ViewBag.Renters = await _context.Users.ToListAsync();
                return View();
            }

            double days = (EndDate - StartDate).TotalDays;
            if (days <= 0) days = 1;
            double totalPrice = days * product.PricePerDay;

            var booking = new Booking
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = ProductId,
                RenterId = RenterId,
                StartDate = StartDate,
                EndDate = EndDate,
                TotalPrice = totalPrice,
                Status = "APPROVED",
                PaymentStatus = "UNPAID",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Bookings.Add(booking);
            product.Status = "RENTED";
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Bookings));
        }

        // GET: /AdminDashboard/EditBooking/5
        public async Task<IActionResult> EditBooking(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            return View(booking);
        }

        // POST: /AdminDashboard/EditBooking/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBooking(string id, [FromForm] DateTime StartDate, [FromForm] DateTime EndDate, [FromForm] string Status, [FromForm] string PaymentStatus, [FromForm] double TotalPrice)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            booking.StartDate = StartDate;
            booking.EndDate = EndDate;
            booking.Status = Status;
            booking.PaymentStatus = PaymentStatus;
            booking.TotalPrice = TotalPrice;
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Bookings));
        }

        // POST: /AdminDashboard/DeleteBooking/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBooking(string id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking != null)
            {
                _context.Bookings.Remove(booking);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Bookings));
        }
    }
}