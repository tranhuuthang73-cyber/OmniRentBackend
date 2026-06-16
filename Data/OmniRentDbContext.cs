using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OmniRentBackend.Models;
using Attribute = OmniRentBackend.Models.Attribute;

namespace OmniRentBackend.Data
{
    public class OmniRentDbContext : DbContext
    {
        public OmniRentDbContext(DbContextOptions<OmniRentDbContext> options) : base(options)
        {
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder
                .Properties<DateTime>()
                .HaveConversion<DateTimeToUnixMillisecondsConverter>();

            configurationBuilder
                .Properties<DateTime?>()
                .HaveConversion<NullableDateTimeToUnixMillisecondsConverter>();
        }

        public class DateTimeToUnixMillisecondsConverter : ValueConverter<DateTime, long>
        {
            public DateTimeToUnixMillisecondsConverter() : base(
                v => new DateTimeOffset(v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v).ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v).UtcDateTime)
            {
            }
        }

        public class NullableDateTimeToUnixMillisecondsConverter : ValueConverter<DateTime?, long?>
        {
            public NullableDateTimeToUnixMillisecondsConverter() : base(
                v => v.HasValue ? new DateTimeOffset(v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value).ToUnixTimeMilliseconds() : null,
                v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value).UtcDateTime : null)
            {
            }
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Attribute> Attributes { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<ProductAttribute> ProductAttributes { get; set; } = null!;
        public DbSet<Booking> Bookings { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<MaintenanceLog> MaintenanceLogs { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<QrCheckIn> QrCheckIns { get; set; } = null!;
        public DbSet<DamageReport> DamageReports { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<UserRole> UserRoles { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<ProductCategory> ProductCategories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure singular table names to match Prisma's naming convention in SQLite
            modelBuilder.Entity<User>().ToTable("User");
            modelBuilder.Entity<Category>().ToTable("Category");
            modelBuilder.Entity<Attribute>().ToTable("Attribute");
            modelBuilder.Entity<Product>().ToTable("Product");
            modelBuilder.Entity<ProductAttribute>().ToTable("ProductAttribute");
            modelBuilder.Entity<Booking>().ToTable("Booking");
            modelBuilder.Entity<Review>().ToTable("Review");
            modelBuilder.Entity<MaintenanceLog>().ToTable("MaintenanceLog");
            modelBuilder.Entity<Message>().ToTable("Message");
            modelBuilder.Entity<Notification>().ToTable("Notification");
            modelBuilder.Entity<QrCheckIn>().ToTable("QrCheckIn");
            modelBuilder.Entity<DamageReport>().ToTable("DamageReport");
            modelBuilder.Entity<Role>().ToTable("Role");
            modelBuilder.Entity<Permission>().ToTable("Permission");
            modelBuilder.Entity<UserRole>().ToTable("UserRole");
            modelBuilder.Entity<RolePermission>().ToTable("RolePermission");
            modelBuilder.Entity<ProductCategory>().ToTable("ProductCategory");

            // User unique constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Category unique slug
            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Slug)
                .IsUnique();

            // Self-referencing Category hierarchy
            modelBuilder.Entity<Category>()
                .HasOne(c => c.Parent)
                .WithMany(c => c.Subcategories)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Message relationships (multiple User foreign keys)
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasIndex(m => new { m.SenderId, m.ReceiverId });

            // Product relationships
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Owner)
                .WithMany(u => u.Products)
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ProductAttribute composite unique constraint
            modelBuilder.Entity<ProductAttribute>()
                .HasIndex(pa => new { pa.ProductId, pa.AttributeId })
                .IsUnique();

            modelBuilder.Entity<ProductAttribute>()
                .HasOne(pa => pa.Product)
                .WithMany(p => p.ProductAttributes)
                .HasForeignKey(pa => pa.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductAttribute>()
                .HasOne(pa => pa.Attribute)
                .WithMany(a => a.ProductAttributes)
                .HasForeignKey(pa => pa.AttributeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Booking relationships
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Product)
                .WithMany(p => p.Bookings)
                .HasForeignKey(b => b.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Renter)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.RenterId)
                .OnDelete(DeleteBehavior.Restrict);

            // Review relationships (FIXED: ProductId no longer cascade)
            modelBuilder.Entity<Review>()
                .HasOne(r => r.Booking)
                .WithMany(b => b.Reviews)
                .HasForeignKey(r => r.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Product)
                .WithMany(p => p.Reviews)
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.NoAction);   // CHANGED: was Cascade -> NoAction

            modelBuilder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // MaintenanceLog relationships
            modelBuilder.Entity<MaintenanceLog>()
                .HasOne(m => m.Product)
                .WithMany(p => p.MaintenanceLogs)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MaintenanceLog>()
                .HasOne(m => m.Owner)
                .WithMany(u => u.MaintenanceLogs)
                .HasForeignKey(m => m.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification relationships
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // QrCheckIn relationships
            modelBuilder.Entity<QrCheckIn>()
                .HasOne(q => q.Booking)
                .WithMany(b => b.QrCheckIns)
                .HasForeignKey(q => q.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            // DamageReport relationships
            modelBuilder.Entity<DamageReport>()
                .HasOne(d => d.Booking)
                .WithMany(b => b.DamageReports)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            // Role configuration
            modelBuilder.Entity<Role>()
                .HasIndex(r => r.Name)
                .IsUnique();

            // Permission configuration
            modelBuilder.Entity<Permission>()
                .HasIndex(p => p.Name)
                .IsUnique();

            // UserRole configuration
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // RolePermission configuration
            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ProductCategory configuration
            modelBuilder.Entity<ProductCategory>()
                .HasKey(pc => new { pc.ProductId, pc.CategoryId });

            modelBuilder.Entity<ProductCategory>()
                .HasOne(pc => pc.Product)
                .WithMany(p => p.ProductCategories)
                .HasForeignKey(pc => pc.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductCategory>()
                .HasOne(pc => pc.Category)
                .WithMany(c => c.ProductCategories)
                .HasForeignKey(pc => pc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}