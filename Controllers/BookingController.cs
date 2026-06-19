using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Data;
using OmniRentBackend.Hubs;
using OmniRentBackend.Models;
using OmniRentBackend.Services;

namespace OmniRentBackend.Controllers
{
    [ApiController]
    [Route("bookings")]
    public class BookingController : ControllerBase
    {
        private readonly OmniRentDbContext _context;
        private readonly TrustService _trustService;
        private readonly AiService _aiService;
        private readonly IHubContext<ChatHub> _hubContext;

        public BookingController(
            OmniRentDbContext context,
            TrustService trustService,
            AiService aiService,
            IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _trustService = trustService;
            _aiService = aiService;
            _hubContext = hubContext;
        }

        private object CalculatePriceDetails(double pricePerDay, DateTime startDate, DateTime endDate)
        {
            var start = startDate.Date;
            var end = endDate.Date;

            var timeDiff = end - start;
            if (timeDiff.TotalDays < 0)
            {
                throw new Exception("Ngày kết thúc phải sau ngày bắt đầu.");
            }

            int durationDays = Math.Max(1, (int)Math.Ceiling(timeDiff.TotalDays));
            double basePrice = Math.Round(pricePerDay * durationDays, 0, MidpointRounding.AwayFromZero);

            return new
            {
                durationDays,
                originalBasePrice = basePrice,
                weekendSurcharge = 0.0,
                holidaySurcharge = 0.0,
                discountRate = 0.0,
                discountAmount = 0.0,
                totalPrice = basePrice
            };
        }

        [HttpPost("estimate-price")]
        public async Task<IActionResult> EstimatePrice([FromBody] EstimatePriceDto dto)
        {
            var product = await _context.Products
                .Include(p => p.Owner)
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId);
            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm." });
            }

            try
            {
                dynamic pricing = CalculatePriceDetails(product.PricePerDay, dto.StartDate, dto.EndDate);
                return Ok(new
                {
                    days = pricing.durationDays,
                    basePrice = pricing.originalBasePrice,
                    discount = pricing.discountAmount,
                    weekendPremium = pricing.weekendSurcharge,
                    totalPrice = pricing.totalPrice
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var product = await _context.Products
                .Include(p => p.Owner)
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId);
            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm." });
            }

            if (product.Status == "MAINTENANCE")
            {
                return BadRequest(new { message = "Sản phẩm đang được bảo trì, không thể thuê." });
            }

            var start = dto.StartDate.Date;
            var end = dto.EndDate.Date;

            // 1. Check overlaps
            var overlapping = await _context.Bookings.AnyAsync(b =>
                b.ProductId == dto.ProductId &&
                (b.Status == "APPROVED" || b.Status == "ONGOING" || b.Status == "PENDING" || b.Status == "WAITING_OWNER_CONFIRM") &&
                b.StartDate <= end && b.EndDate >= start);

            if (overlapping)
            {
                return BadRequest(new { message = "Thời gian này sản phẩm đã có lịch đặt hoặc đang được thuê." });
            }

            // 2. Check maintenance overlaps
            var overlappingMaint = await _context.MaintenanceLogs.AnyAsync(m =>
                m.ProductId == dto.ProductId &&
                m.Status == "UNDER_REPAIR" &&
                m.StartDate <= end && (m.EndDate == null || m.EndDate >= start));

            if (overlappingMaint)
            {
                return BadRequest(new { message = "Sản phẩm đã được lên lịch bảo trì trong khoảng thời gian này." });
            }

            dynamic pricing = CalculatePriceDetails(product.PricePerDay, start, end);
            double totalPrice = pricing.totalPrice;
            double depositAmount = Math.Round(totalPrice * 0.5, 0, MidpointRounding.AwayFromZero);
            double remainingAmount = totalPrice - depositAmount;
            var transferContent = $"COC OMNIRENT {Guid.NewGuid().ToString("N")[..8].ToUpper()}";

            var booking = new Booking
            {
                ProductId = dto.ProductId,
                RenterId = userId,
                StartDate = start,
                EndDate = end,
                TotalPrice = totalPrice,
                DepositAmount = depositAmount,
                RemainingAmount = remainingAmount,
                TransferContent = transferContent,
                Status = "PENDING",
                PaymentStatus = "UNPAID",
                RentalAddress = product.Owner?.PickupAddress
                    ?? product.Owner?.Address
                    ?? dto.RentalAddress
            };

            _context.Bookings.Add(booking);

            // Create notification for owner
            var notif = new Notification
            {
                UserId = product.OwnerId,
                Title = "Yêu cầu thuê mới",
                Content = $"Bạn có một yêu cầu thuê mới cho sản phẩm: \"{product.Name}\"."
            };
            _context.Notifications.Add(notif);

            await _context.SaveChangesAsync();

            // Push notification via SignalR
            await SendLiveNotificationAsync(product.OwnerId, notif);

            return Ok(booking);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBookings()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var role = User.FindFirst("role")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            List<Booking> bookings;

            if (role == "ADMIN")
            {
                bookings = await _context.Bookings
                    .Include(b => b.Product)
                        .ThenInclude(p => p!.Owner)
                    .Include(b => b.Renter)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync();
            }
            else if (role == "OWNER")
            {
                bookings = await _context.Bookings
                    .Include(b => b.Product)
                    .Include(b => b.Renter)
                    .Where(b => b.Product != null && b.Product.OwnerId == userId)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync();
            }
            else
            {
                bookings = await _context.Bookings
                    .Include(b => b.Product)
                        .ThenInclude(p => p!.Owner)
                    .Include(b => b.Renter)
                    .Where(b => b.RenterId == userId)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync();
            }

            return Ok(bookings);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBooking(string id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Product)
                    .ThenInclude(p => p!.Owner)
                .Include(b => b.Renter)
                .Include(b => b.QrCheckIns)
                .Include(b => b.DamageReports)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound(new { message = "Không tìm thấy đơn thuê." });
            }

            return Ok(booking);
        }

        [Authorize]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateStatusDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var role = User.FindFirst("role")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound(new { message = "Không tìm thấy đơn thuê." });
            }

            bool isOwner = booking.Product?.OwnerId == userId;
            bool isRenter = booking.RenterId == userId;
            bool isAdmin = role == "ADMIN";

            if (!isOwner && !isRenter && !isAdmin)
            {
                return Forbid();
            }

            booking.Status = dto.Status;
            if (dto.Status == "COMPLETED")
            {
                booking.RemainingPaid = true;
                booking.CompletedAt = DateTime.UtcNow;
                booking.PaymentStatus = "PAID_FULL";
            }
            else if (dto.Status == "CANCELLED" && booking.PaymentStatus == "PAID")
            {
                booking.PaymentStatus = "REFUNDED";
            }

            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Create notification for other party
            string notifyUserId = isOwner ? booking.RenterId : booking.Product!.OwnerId;
            var notif = new Notification
            {
                UserId = notifyUserId,
                Title = "Cập nhật đơn thuê",
                Content = $"Đơn thuê \"{booking.Product?.Name}\" đã được cập nhật thành: {dto.Status}."
            };
            _context.Notifications.Add(notif);
            await _context.SaveChangesAsync();

            await SendLiveNotificationAsync(notifyUserId, notif);

            // Trigger booking update event
            await BroadcastBookingUpdateAsync(booking.RenterId, booking);
            if (booking.Product != null)
            {
                await BroadcastBookingUpdateAsync(booking.Product.OwnerId, booking);
            }

            // Recalculate renter trust score
            if (dto.Status == "COMPLETED" || dto.Status == "CANCELLED")
            {
                await _trustService.RecalculateRenterScoreAsync(booking.RenterId);
            }

            return Ok(booking);
        }

        [Authorize]
        [HttpPost("{id}/qr")]
        public async Task<IActionResult> GenerateQr(string id, [FromBody] GenerateQrDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound(new { message = "Không tìm thấy đơn thuê." });
            }

            if (booking.RenterId != userId && booking.Product?.OwnerId != userId)
            {
                return BadRequest(new { message = "Bạn không có quyền tạo mã QR cho đơn này." });
            }

            string qrHash = $"OMNIRENT-BKG-{id}-{dto.Type}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            var qrLog = new QrCheckIn
            {
                BookingId = id,
                QrCodeString = qrHash,
                CheckInType = dto.Type,
                ScannedAt = DateTime.UtcNow
            };

            _context.QrCheckIns.Add(qrLog);
            await _context.SaveChangesAsync();

            return Ok(new { qrCodeString = qrHash });
        }

        [Authorize]
        [HttpPost("qr-scan")]
        public async Task<IActionResult> ProcessQrScan([FromBody] ProcessQrScanDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var qrLog = await _context.QrCheckIns
                .Include(q => q.Booking)
                    .ThenInclude(b => b!.Product)
                .FirstOrDefaultAsync(q => q.QrCodeString == dto.QrCodeString);

            if (qrLog == null || qrLog.Booking == null)
            {
                return BadRequest(new { message = "Mã QR không hợp lệ hoặc đã hết hạn." });
            }

            var booking = qrLog.Booking;
            if (booking.Product?.OwnerId != userId)
            {
                return BadRequest(new { message = "Bạn không phải chủ sản phẩm của đơn thuê này." });
            }

            string nextStatus = booking.Status;
            if (qrLog.CheckInType == "CHECKIN")
            {
                if (booking.Status != "APPROVED" && booking.Status != "PENDING")
                {
                    return BadRequest(new { message = "Đơn thuê phải ở trạng thái APPROVED để Check-in." });
                }
                nextStatus = "ONGOING";

                // Mark product as rented
                var product = await _context.Products.FindAsync(booking.ProductId);
                if (product != null)
                {
                    product.Status = "RENTED";
                    product.UpdatedAt = DateTime.UtcNow;
                }
            }
            else if (qrLog.CheckInType == "CHECKOUT")
            {
                if (booking.Status != "ONGOING")
                {
                    return BadRequest(new { message = "Đơn thuê phải ở trạng thái ONGOING để Check-out." });
                }
                nextStatus = "COMPLETED";

                // Mark product as available
                var product = await _context.Products.FindAsync(booking.ProductId);
                if (product != null)
                {
                    product.Status = "AVAILABLE";
                    product.UpdatedAt = DateTime.UtcNow;
                }
            }

            booking.Status = nextStatus;
            if (qrLog.CheckInType == "CHECKOUT")
            {
                booking.RemainingPaid = true;
                booking.CompletedAt = DateTime.UtcNow;
                booking.PaymentStatus = "PAID_FULL";
            }
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Create notification for renter
            var notif = new Notification
            {
                UserId = booking.RenterId,
                Title = qrLog.CheckInType == "CHECKIN" ? "Nhận đồ thành công" : "Trả đồ thành công",
                Content = $"Đơn thuê \"{booking.Product?.Name}\" đã được chủ đồ quét QR xác nhận {(qrLog.CheckInType == "CHECKIN" ? "Check-in" : "Check-out")}."
            };
            _context.Notifications.Add(notif);
            await _context.SaveChangesAsync();

            await SendLiveNotificationAsync(booking.RenterId, notif);

            // Broadcast booking update
            await BroadcastBookingUpdateAsync(booking.RenterId, booking);
            await BroadcastBookingUpdateAsync(userId, booking);

            // Recalculate trust score
            await _trustService.RecalculateRenterScoreAsync(booking.RenterId);

            return Ok(booking);
        }

        [Authorize]
        [HttpPost("{id}/damage-report")]
        public async Task<IActionResult> UploadDamageReport(string id, [FromBody] UploadDamageReportDto dto)
        {
            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound(new { message = "Không tìm thấy đơn thuê." });
            }

            dynamic aiAnalysis = await _aiService.ScanDamageImageAsync(dto.ImageUrl);

            var report = new DamageReport
            {
                BookingId = id,
                ImageUrl = dto.ImageUrl,
                Severity = aiAnalysis.severity,
                Details = aiAnalysis.details,
                RepairEstimate = aiAnalysis.repairEstimate,
                CreatedAt = DateTime.UtcNow
            };

            _context.DamageReports.Add(report);
            await _context.SaveChangesAsync();

            if (aiAnalysis.severity != "NONE" && booking.Product != null)
            {
                // Move product to maintenance
                var product = await _context.Products.FindAsync(booking.ProductId);
                if (product != null)
                {
                    product.Status = "MAINTENANCE";
                    product.UpdatedAt = DateTime.UtcNow;
                }

                var log = new MaintenanceLog
                {
                    ProductId = booking.ProductId,
                    OwnerId = booking.Product.OwnerId,
                    IssueDescription = $"[AI Damage Report]: Severity {aiAnalysis.severity}. Details: {aiAnalysis.details}",
                    Cost = aiAnalysis.repairEstimate,
                    StartDate = DateTime.UtcNow,
                    Status = "UNDER_REPAIR"
                };
                _context.MaintenanceLogs.Add(log);

                // Notification to owner
                var ownerNotif = new Notification
                {
                    UserId = booking.Product.OwnerId,
                    Title = "Sản phẩm bị hư hại - Đã chuyển sang bảo trì",
                    Content = $"AI phát hiện hư hỏng ở mức độ {aiAnalysis.severity} cho \"{booking.Product.Name}\". Ước tính chi phí sửa chữa: {aiAnalysis.repairEstimate.ToString("N0")}đ."
                };
                _context.Notifications.Add(ownerNotif);
                await _context.SaveChangesAsync();

                await SendLiveNotificationAsync(booking.Product.OwnerId, ownerNotif);
            }

            return Ok(new
            {
                report,
                aiAnalysis
            });
        }

        [HttpGet("product/{productId}/blocked-dates")]
        public async Task<IActionResult> GetBlockedDates(string productId)
        {
            var bookings = await _context.Bookings
                .Where(b => b.ProductId == productId && (b.Status == "APPROVED" || b.Status == "ONGOING"))
                .Select(b => new { b.StartDate, b.EndDate })
                .ToListAsync();

            var maintenance = await _context.MaintenanceLogs
                .Where(m => m.ProductId == productId && m.Status == "UNDER_REPAIR")
                .Select(m => new { m.StartDate, m.EndDate })
                .ToListAsync();

            var blocked = new List<object>();

            foreach (var b in bookings)
            {
                blocked.Add(new { start = b.StartDate, end = b.EndDate, type = "RENTAL" });
            }

            foreach (var m in maintenance)
            {
                blocked.Add(new { start = m.StartDate, end = m.EndDate ?? DateTime.UtcNow, type = "MAINTENANCE" });
            }

            return Ok(blocked);
        }

        [Authorize]
        [HttpPut("{id}/pay-deposit")]
        public async Task<IActionResult> PayDeposit(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound(new { message = "Không tìm thấy đơn thuê." });
            }

            if (booking.RenterId != userId)
            {
                return Forbid();
            }

            if (booking.PaymentStatus == "PAID" || booking.PaymentStatus == "PAID_FULL" || booking.PaymentStatus == "WAITING_CONFIRMATION")
            {
                return BadRequest(new { message = "Đơn thuê đã được thanh toán cọc." });
            }

            if (booking.DepositAmount <= 0)
            {
                booking.DepositAmount = Math.Round(booking.TotalPrice * 0.5, 0, MidpointRounding.AwayFromZero);
                booking.RemainingAmount = booking.TotalPrice - booking.DepositAmount;
            }

            booking.PaymentStatus = "WAITING_CONFIRMATION";
            booking.Status = "WAITING_OWNER_CONFIRM";
            booking.UpdatedAt = DateTime.UtcNow;

            // Notification for owner
            var ownerNotif = new Notification
            {
                UserId = booking.Product!.OwnerId,
                Title = "Đã nộp đặt cọc",
                Content = $"Khách thuê đã thanh toán cọc thành công cho sản phẩm \"{booking.Product.Name}\"."
            };
            _context.Notifications.Add(ownerNotif);

            await _context.SaveChangesAsync();

            await SendLiveNotificationAsync(booking.Product.OwnerId, ownerNotif);

            // Broadcast booking update
            await BroadcastBookingUpdateAsync(booking.RenterId, booking);
            await BroadcastBookingUpdateAsync(booking.Product.OwnerId, booking);

            return Ok(booking);
        }

        [Authorize(Roles = "OWNER,ADMIN")]
        [HttpPut("{id}/confirm-deposit")]
        public async Task<IActionResult> ConfirmDeposit(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var role = User.FindFirst("role")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound(new { message = "Khong tim thay don thue." });
            }

            if (role != "ADMIN" && booking.Product?.OwnerId != userId)
            {
                return Forbid();
            }

            if (booking.PaymentStatus != "WAITING_CONFIRMATION")
            {
                return BadRequest(new { message = "Don nay chua o trang thai cho xac nhan coc." });
            }

            booking.DepositPaid = true;
            booking.DepositPaidAt = DateTime.UtcNow;
            booking.PaymentStatus = "PAID";
            booking.Status = "APPROVED";
            booking.UpdatedAt = DateTime.UtcNow;

            var renterNotif = new Notification
            {
                UserId = booking.RenterId,
                Title = "Tien coc da duoc xac nhan",
                Content = $"Chu cho thue da xac nhan tien coc cho \"{booking.Product?.Name}\". Ban co the den nhan do theo lich."
            };
            _context.Notifications.Add(renterNotif);

            await _context.SaveChangesAsync();
            await SendLiveNotificationAsync(booking.RenterId, renterNotif);
            await BroadcastBookingUpdateAsync(booking.RenterId, booking);
            if (booking.Product != null)
            {
                await BroadcastBookingUpdateAsync(booking.Product.OwnerId, booking);
            }

            return Ok(booking);
        }

        [Authorize(Roles = "OWNER,ADMIN")]
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteBooking(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var role = User.FindFirst("role")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound(new { message = "Khong tim thay don thue." });
            }

            if (role != "ADMIN" && booking.Product?.OwnerId != userId)
            {
                return Forbid();
            }

            booking.Status = "COMPLETED";
            booking.RemainingPaid = true;
            booking.CompletedAt = DateTime.UtcNow;
            booking.PaymentStatus = "PAID_FULL";
            booking.UpdatedAt = DateTime.UtcNow;

            if (booking.Product != null)
            {
                booking.Product.Status = "AVAILABLE";
                booking.Product.UpdatedAt = DateTime.UtcNow;
            }

            var renterNotif = new Notification
            {
                UserId = booking.RenterId,
                Title = "Don thue da hoan tat",
                Content = $"Don thue \"{booking.Product?.Name}\" da duoc xac nhan hoan tat."
            };
            _context.Notifications.Add(renterNotif);

            await _context.SaveChangesAsync();
            await SendLiveNotificationAsync(booking.RenterId, renterNotif);
            await BroadcastBookingUpdateAsync(booking.RenterId, booking);
            if (booking.Product != null)
            {
                await BroadcastBookingUpdateAsync(booking.Product.OwnerId, booking);
            }

            return Ok(booking);
        }

        private async Task SendLiveNotificationAsync(string userId, Notification notif)
        {
            if (ChatHub.ActiveConnections.TryGetValue(userId, out var connId))
            {
                await _hubContext.Clients.Client(connId).SendAsync("newNotification", new
                {
                    id = notif.Id,
                    userId = notif.UserId,
                    title = notif.Title,
                    content = notif.Content,
                    isRead = notif.IsRead,
                    createdAt = notif.CreatedAt
                });
            }
        }

        private async Task BroadcastBookingUpdateAsync(string userId, Booking booking)
        {
            if (ChatHub.ActiveConnections.TryGetValue(userId, out var connId))
            {
                await _hubContext.Clients.Client(connId).SendAsync("bookingUpdated", new
                {
                    id = booking.Id,
                    status = booking.Status,
                    paymentStatus = booking.PaymentStatus,
                    depositPaid = booking.DepositPaid
                });
            }
        }
    }

    public class EstimatePriceDto
    {
        public string ProductId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class CreateBookingDto
    {
        public string ProductId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? RentalAddress { get; set; }
    }

    public class UpdateStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }

    public class GenerateQrDto
    {
        public string Type { get; set; } = "CHECKIN"; // CHECKIN, CHECKOUT
    }

    public class ProcessQrScanDto
    {
        public string QrCodeString { get; set; } = string.Empty;
    }

    public class UploadDamageReportDto
    {
        public string ImageUrl { get; set; } = string.Empty;
    }
}
