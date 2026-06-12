using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace OmniRentBackend.Models
{
    public class User
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string Role { get; set; } = "RENTER"; // ADMIN, OWNER, RENTER

        public string? AvatarUrl { get; set; }

        public double RenterTrustScore { get; set; } = 80.0;

        public bool OwnerVerified { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        [JsonIgnore]
        public ICollection<Product> Products { get; set; } = new List<Product>();

        [JsonIgnore]
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        [JsonIgnore]
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        [JsonIgnore]
        public ICollection<MaintenanceLog> MaintenanceLogs { get; set; } = new List<MaintenanceLog>();

        [JsonIgnore]
        public ICollection<Message> SentMessages { get; set; } = new List<Message>();

        [JsonIgnore]
        public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();

        [JsonIgnore]
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    public class Category
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Slug { get; set; } = string.Empty;

        public string? ParentId { get; set; }

        [ForeignKey("ParentId")]
        public Category? Parent { get; set; }

        public ICollection<Category> Subcategories { get; set; } = new List<Category>();

        [JsonIgnore]
        public ICollection<Attribute> Attributes { get; set; } = new List<Attribute>();

        [JsonIgnore]
        public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();

        [JsonIgnore]
        public ICollection<Product> Products { get; set; } = new List<Product>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Attribute
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = "TEXT"; // TEXT, NUMBER, SELECT

        public string? Options { get; set; } // Comma-separated list for select types

        [Required]
        public string CategoryId { get; set; } = string.Empty;

        [ForeignKey("CategoryId")]
        [JsonIgnore]
        public Category? Category { get; set; }

        [JsonIgnore]
        public ICollection<ProductAttribute> ProductAttributes { get; set; } = new List<ProductAttribute>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Product
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public double PricePerDay { get; set; }

        public double DepositAmount { get; set; }

        [Required]
        public string CategoryId { get; set; } = string.Empty;

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        [Required]
        public string OwnerId { get; set; } = string.Empty;

        [ForeignKey("OwnerId")]
        public User? Owner { get; set; }

        public string ImagesJson { get; set; } = "[]"; // JSON string array of URLs

        public string Status { get; set; } = "PENDING_APPROVAL"; // PENDING_APPROVAL, AVAILABLE, RENTED, MAINTENANCE

        public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();

        public ICollection<ProductAttribute> ProductAttributes { get; set; } = new List<ProductAttribute>();

        [JsonIgnore]
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        [JsonIgnore]
        public ICollection<MaintenanceLog> MaintenanceLogs { get; set; } = new List<MaintenanceLog>();

        [JsonIgnore]
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ProductAttribute
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ProductId { get; set; } = string.Empty;

        [ForeignKey("ProductId")]
        [JsonIgnore]
        public Product? Product { get; set; }

        [Required]
        public string AttributeId { get; set; } = string.Empty;

        [ForeignKey("AttributeId")]
        public Attribute? Attribute { get; set; }

        [Required]
        public string Value { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Booking
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ProductId { get; set; } = string.Empty;

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Required]
        public string RenterId { get; set; } = string.Empty;

        [ForeignKey("RenterId")]
        public User? Renter { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public double TotalPrice { get; set; }

        public bool DepositPaid { get; set; } = false;

        public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, ONGOING, COMPLETED, REJECTED, CANCELLED

        public string PaymentStatus { get; set; } = "UNPAID"; // UNPAID, PAID, REFUNDED

        public string? RentalAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        [JsonIgnore]
        public ICollection<QrCheckIn> QrCheckIns { get; set; } = new List<QrCheckIn>();

        [JsonIgnore]
        public ICollection<DamageReport> DamageReports { get; set; } = new List<DamageReport>();
    }

    public class Review
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string BookingId { get; set; } = string.Empty;

        [ForeignKey("BookingId")]
        [JsonIgnore]
        public Booking? Booking { get; set; }

        [Required]
        public string ProductId { get; set; } = string.Empty;

        [ForeignKey("ProductId")]
        [JsonIgnore]
        public Product? Product { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public User? User { get; set; }

        public int Rating { get; set; }

        [Required]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class MaintenanceLog
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ProductId { get; set; } = string.Empty;

        [ForeignKey("ProductId")]
        [JsonIgnore]
        public Product? Product { get; set; }

        [Required]
        public string OwnerId { get; set; } = string.Empty;

        [ForeignKey("OwnerId")]
        public User? Owner { get; set; }

        [Required]
        public string IssueDescription { get; set; } = string.Empty;

        public double Cost { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string Status { get; set; } = "UNDER_REPAIR"; // UNDER_REPAIR, RESOLVED

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Message
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string SenderId { get; set; } = string.Empty;

        [ForeignKey("SenderId")]
        public User? Sender { get; set; }

        [Required]
        public string ReceiverId { get; set; } = string.Empty;

        [ForeignKey("ReceiverId")]
        public User? Receiver { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Notification
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        [JsonIgnore]
        public User? User { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class QrCheckIn
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string BookingId { get; set; } = string.Empty;

        [ForeignKey("BookingId")]
        [JsonIgnore]
        public Booking? Booking { get; set; }

        [Required]
        public string QrCodeString { get; set; } = string.Empty;

        [Required]
        public string CheckInType { get; set; } = string.Empty; // CHECKIN, CHECKOUT

        public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    }

    public class DamageReport
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string BookingId { get; set; } = string.Empty;

        [ForeignKey("BookingId")]
        [JsonIgnore]
        public Booking? Booking { get; set; }

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        [Required]
        public string Severity { get; set; } = string.Empty; // NONE, LIGHT, SEVERE

        [Required]
        public string Details { get; set; } = string.Empty;

        public double RepairEstimate { get; set; } = 0.0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Role
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        [JsonIgnore]
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }

    public class Permission
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }

    public class UserRole
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        [JsonIgnore]
        public User? User { get; set; }

        [Required]
        public string RoleId { get; set; } = string.Empty;

        [ForeignKey("RoleId")]
        public Role? Role { get; set; }
    }

    public class RolePermission
    {
        [Required]
        public string RoleId { get; set; } = string.Empty;

        [ForeignKey("RoleId")]
        [JsonIgnore]
        public Role? Role { get; set; }

        [Required]
        public string PermissionId { get; set; } = string.Empty;

        [ForeignKey("PermissionId")]
        public Permission? Permission { get; set; }
    }

    public class ProductCategory
    {
        [Required]
        public string ProductId { get; set; } = string.Empty;

        [ForeignKey("ProductId")]
        [JsonIgnore]
        public Product? Product { get; set; }

        [Required]
        public string CategoryId { get; set; } = string.Empty;

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }
    }
}
