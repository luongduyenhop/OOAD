using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchedulingApp.Services;

namespace SchedulingApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;

        public AccountController(IAuthService authService)
        {
            _authService = authService;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login() => View();

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (await _authService.LoginAsync(email, password))
                return RedirectToAction("Index", "Tasks");
            
            ViewBag.Error = "Email hoặc mật khẩu không chính xác.";
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register() => View();

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string fullName, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                ViewBag.Error = "Vui lòng nhập họ và tên.";
                return View();
            }

            if (!new EmailAddressAttribute().IsValid(email))
            {
                ViewBag.Error = "Email không hợp lệ.";
                return View();
            }

            var result = await _authService.RegisterAsync(fullName, email, password);
            if (result.Success)
            {
                return RedirectToAction("Login");
            }
            
            ViewBag.Error = result.Error ?? "Đăng ký thất bại.";
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login");

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                await _authService.UpdateProfileAsync(userId.Value, fullName);
            }
            return RedirectToAction("Index", "Tasks");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync();
            return RedirectToAction("Login");
        }
    }
}
