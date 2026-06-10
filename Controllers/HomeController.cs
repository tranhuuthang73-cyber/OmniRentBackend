using Microsoft.AspNetCore.Mvc;

namespace OmniRentBackend.Controllers
{
    /// <summary>
    /// Trang chủ chung — hiển thị cho tất cả người dùng.
    /// </summary>
    public class HomeController : Controller
    {
        // GET /  hoặc  /Home/Index
        public IActionResult Index()
        {
            return View();
        }
    }
}
