using Microsoft.AspNetCore.Mvc;

namespace OmniRentBackend.Controllers
{
    /// <summary>
    /// Dashboard dành cho Owner — chỉ tài khoản có Role "OWNER" mới được điều hướng vào đây.
    /// </summary>
    public class OwnerDashboardController : Controller
    {
        // GET /OwnerDashboard
        public IActionResult Index()
        {
            return View();
        }
    }
}
