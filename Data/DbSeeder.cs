using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OmniRentBackend.Models;
using Attribute = OmniRentBackend.Models.Attribute;

namespace OmniRentBackend.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(OmniRentDbContext context)
        {
            await context.Database.EnsureCreatedAsync();

            // 1. Seed Users if empty
            if (!context.Users.Any())
            {
                var adminPassword = BCrypt.Net.BCrypt.HashPassword("admin123");
                var ownerPassword = BCrypt.Net.BCrypt.HashPassword("owner123");
                var renterPassword = BCrypt.Net.BCrypt.HashPassword("renter123");

                var admin = new User
                {
                    Email = "admin@omnirent.com",
                    PasswordHash = adminPassword,
                    FullName = "Hệ Thống Admin",
                    Phone = "0987654321",
                    Role = "ADMIN",
                    AvatarUrl = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=150&h=150&q=80",
                    OwnerVerified = true,
                    RenterTrustScore = 100.0
                };

                var owner = new User
                {
                    Email = "owner@omnirent.com",
                    PasswordHash = ownerPassword,
                    FullName = "Lê Văn Chủ Đồ",
                    Phone = "0912345678",
                    Role = "OWNER",
                    AvatarUrl = "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?auto=format&fit=crop&w=150&h=150&q=80",
                    OwnerVerified = true,
                    RenterTrustScore = 85.0
                };

                var renter = new User
                {
                    Email = "renter@omnirent.com",
                    PasswordHash = renterPassword,
                    FullName = "Nguyễn Thuê Đồ",
                    Phone = "0909090909",
                    Role = "RENTER",
                    AvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=150&h=150&q=80",
                    OwnerVerified = false,
                    RenterTrustScore = 95.0
                };

                context.Users.AddRange(admin, owner, renter);
                await context.SaveChangesAsync();
            }

            // 2. Seed root categories if empty
            if (!context.Categories.Any())
            {
                var mobility = new Category { Name = "Mobility", Slug = "mobility" };
                var techGear = new Category { Name = "Tech Gear", Slug = "tech-gear" };
                var apparel = new Category { Name = "Apparel", Slug = "apparel" };

                context.Categories.AddRange(mobility, techGear, apparel);
                await context.SaveChangesAsync();

                // Subcategories
                var xeMay = new Category { Name = "Xe máy", Slug = "xe-may", ParentId = mobility.Id };
                var xeDien = new Category { Name = "Xe điện", Slug = "xe-dien", ParentId = mobility.Id };
                var xeDap = new Category { Name = "Xe đạp", Slug = "xe-dap", ParentId = mobility.Id };

                var laptop = new Category { Name = "Laptop", Slug = "laptop", ParentId = techGear.Id };
                var mayAnh = new Category { Name = "Máy ảnh", Slug = "may-anh", ParentId = techGear.Id };
                var mayChieu = new Category { Name = "Máy chiếu", Slug = "may-chieu", ParentId = techGear.Id };
                var loa = new Category { Name = "Loa", Slug = "loa", ParentId = techGear.Id };

                var vayTiec = new Category { Name = "Váy tiệc", Slug = "vay-tiec", ParentId = apparel.Id };
                var vest = new Category { Name = "Vest", Slug = "vest", ParentId = apparel.Id };
                var cosplay = new Category { Name = "Đồ cosplay", Slug = "cosplay", ParentId = apparel.Id };

                context.Categories.AddRange(xeMay, xeDien, xeDap, laptop, mayAnh, mayChieu, loa, vayTiec, vest, cosplay);
                await context.SaveChangesAsync();

                // 3. Seed Attributes
                context.Attributes.AddRange(
                    new Attribute { Name = "Phân khối", Type = "TEXT", CategoryId = xeMay.Id },
                    new Attribute { Name = "Hộp số", Type = "SELECT", Options = "Tự động (Ga),Số sàn (Côn tay),Số sàn (Số chân)", CategoryId = xeMay.Id },
                    new Attribute { Name = "Nhiên liệu", Type = "SELECT", Options = "Xăng,Điện", CategoryId = xeMay.Id },
                    new Attribute { Name = "Quãng đường tối đa", Type = "TEXT", CategoryId = xeDien.Id },
                    new Attribute { Name = "Tốc độ tối đa", Type = "TEXT", CategoryId = xeDien.Id },
                    new Attribute { Name = "Loại xe đạp", Type = "SELECT", Options = "Địa hình (MTB),Đường phố (City),Đường trường (Road)", CategoryId = xeDap.Id },
                    new Attribute { Name = "Khung xe", Type = "SELECT", Options = "Nhôm,Carbon,Thép", CategoryId = xeDap.Id },
                    new Attribute { Name = "CPU", Type = "TEXT", CategoryId = laptop.Id },
                    new Attribute { Name = "RAM", Type = "SELECT", Options = "8 GB,16 GB,32 GB,64 GB", CategoryId = laptop.Id },
                    new Attribute { Name = "Ổ cứng", Type = "SELECT", Options = "256 GB,512 GB,1 TB,2 TB", CategoryId = laptop.Id },
                    new Attribute { Name = "Loại cảm biến", Type = "SELECT", Options = "Full Frame,APS-C,Medium Format", CategoryId = mayAnh.Id },
                    new Attribute { Name = "Độ phân giải (MP)", Type = "NUMBER", CategoryId = mayAnh.Id },
                    new Attribute { Name = "Độ phân giải", Type = "SELECT", Options = "Full HD (1080p),4K UHD", CategoryId = mayChieu.Id },
                    new Attribute { Name = "Độ sáng (ANSI)", Type = "TEXT", CategoryId = mayChieu.Id },
                    new Attribute { Name = "Công suất (W)", Type = "TEXT", CategoryId = loa.Id },
                    new Attribute { Name = "Kích cỡ váy", Type = "SELECT", Options = "XS,S,M,L,XL", CategoryId = vayTiec.Id },
                    new Attribute { Name = "Kích cỡ vest", Type = "SELECT", Options = "S,M,L,XL,XXL", CategoryId = vest.Id },
                    new Attribute { Name = "Nhân vật", Type = "TEXT", CategoryId = cosplay.Id },
                    new Attribute { Name = "Kích cỡ cosplay", Type = "SELECT", Options = "S,M,L,XL", CategoryId = cosplay.Id }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}
