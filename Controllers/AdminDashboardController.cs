using Microsoft.AspNetCore.Mvc;

namespace OmniRentBackend.Controllers
{
    /// <summary>
    /// Dashboard dành cho Admin — chỉ tài khoản có Role "ADMIN" mới được điều hướng vào đây.
    /// </summary>
    public class AdminDashboardController : Controller
    {
        // GET /AdminDashboard
        public IActionResult Index()
        {
            return View();
        }
    }
}