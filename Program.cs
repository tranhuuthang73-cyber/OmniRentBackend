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

// Force listening on port 5285
builder.WebHost.UseUrls("http://0.0.0.0:5285");

// 1. Configure EF Core with SQL Server (connection string từ appsettings.json)
builder.Services.AddDbContext<OmniRentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Add Custom Services
builder.Services.AddScoped<AiService>();
builder.Services.AddScoped<TrustService>();

// 3. Add Controllers + Razor Views (MVC) and JSON options
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 4. SignalR
builder.Services.AddSignalR();

// 5. Authentication: Cookie + JWT
var secret = builder.Configuration["Jwt:Secret"] ?? "OmniRentSuperSecretStartupJwtTokenKey2026!!!";
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
})
// Cookie tạm thời để lưu thông tin Google sau OAuth, trước khi tạo session chính
.AddCookie("ExternalCookie", options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = ".AspNetCore.ExternalCookie";
})
.AddJwtBearer(options =>
{
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
})
.AddGoogle(options =>
{
    var googleAuthNSection = builder.Configuration.GetSection("Authentication:Google");
    
    options.ClientId = googleAuthNSection["ClientId"] ?? throw new InvalidOperationException("Google ClientId is missing.");
    options.ClientSecret = googleAuthNSection["ClientSecret"] ?? throw new InvalidOperationException("Google ClientSecret is missing.");
    // Lưu kết quả OAuth vào cookie tạm, không phải cookie chính
    options.SignInScheme = "ExternalCookie";
    // Ensure correlation cookie settings are compatible with common browsers and local dev (HTTP)
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.CorrelationCookie.Name = ".AspNetCore.Correlation.Google";
    // Explicit callback path (default is /signin-google) kept for clarity
    options.CallbackPath = "/signin-google";
});

builder.Services.AddAuthorization();

// 6. CORS
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

// 7. Seed database (gọi đồng bộ an toàn)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<OmniRentDbContext>();
        // Đảm bảo database đã được tạo (tự động nếu chưa có)
        context.Database.Migrate(); // Chạy migration nếu có
        Task.Run(async () => await DbSeeder.SeedAsync(context)).GetAwaiter().GetResult();
        Console.WriteLine("Database check and seeding completed successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred during database seeding: {ex.Message}");
    }
}

app.UseCors("CorsPolicy");
app.UseStaticFiles();
// Ensure cookie policy is applied so SameSite settings take effect for OAuth flow
app.UseCookiePolicy(new CookiePolicyOptions { MinimumSameSitePolicy = SameSiteMode.Lax });
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OmniRentBackend.Middleware.PermissionMiddleware>();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<ChatHub>("/hubs/chat");

app.Run();