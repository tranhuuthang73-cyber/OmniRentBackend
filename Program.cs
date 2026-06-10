using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OmniRentBackend.Data;
using OmniRentBackend.Hubs;
using OmniRentBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// Force listening on port 5285 to match the original NestJS backend
builder.WebHost.UseUrls("http://0.0.0.0:5285");

// 1. Configure EF Core Database Context (SQLite)
// Use the local SQLite database path
string dbPath = "Data Source=dev.db";
builder.Services.AddDbContext<OmniRentDbContext>(options =>
    options.UseSqlite(dbPath));

// 2. Add Custom Services
builder.Services.AddScoped<AiService>();
builder.Services.AddScoped<TrustService>();

// 3. Add Controllers + Razor Views (MVC) và configure JSON options
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Prevent infinite loops on circular relational dependencies
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 4. Configure SignalR for real-time chat and alerts
builder.Services.AddSignalR();

// 5. Configure Authentication: Cookie (for browser/MVC) + JWT Bearer (for API clients)
var secret = builder.Configuration["Jwt:Secret"] ?? "OmniRentSuperSecretStartupJwtTokenKey2026!!!";
builder.Services.AddAuthentication(options =>
{
    // Cookie là scheme mặc định cho trình duyệt (MVC Razor Views)
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/auth/login";     // Redirect đến trang login nếu chưa xác thực
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;     // Gia hạn cookie nếu user còn hoạt động
    options.Cookie.HttpOnly = true;       // Bảo vệ cookie khỏi JavaScript
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
})
.AddJwtBearer(options =>
{
    // JWT vẫn giữ nguyên cho API clients (Postman, mobile app, v.v.)
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true
    };
});

builder.Services.AddAuthorization();

// 6. Configure CORS to allow any origin (e.g. localhost:3000, 0.0.0.0:3000) with credentials
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// 7. Seed Database on startup if tables are empty
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<OmniRentDbContext>();
        await DbSeeder.SeedAsync(context);
        Console.WriteLine("Database check and seeding completed successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred during database seeding: {ex.Message}");
    }
}

app.UseCors("CorsPolicy");

// Phục vụ các file tĩnh (Bootstrap CSS/JS, ảnh, v.v.) từ wwwroot/
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Đăng ký Permission Middleware cho hệ thống phân quyền RBAC
app.UseMiddleware<OmniRentBackend.Middleware.PermissionMiddleware>();

// Map API controllers (attribute routing)
app.MapControllers();

// Map MVC conventional routes (cho Razor Views)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR Hub
app.MapHub<ChatHub>("/hubs/chat");

// Ghi chú: endpoint tĩnh "/" đã được xóa.
// Trang gốc "/" nay được xử lý bởi HomeController.Index
// thông qua MapControllerRoute bên trên.

app.Run();
