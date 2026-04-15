using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using SchedulingApp.Models;

namespace SchedulingApp.Services
{
    public interface IAuthService
    {
        Task<bool> LoginAsync(string username, string password);
        Task<(bool Success, string? Error)> RegisterAsync(string username, string password);
        Task LogoutAsync();
        int? GetCurrentUserId();
        string? GetCurrentUserName();
        bool IsAuthenticated();
    }

    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                return false;
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                password,
                isPersistent: false,
                lockoutOnFailure: false);
            return result.Succeeded;
        }

        public async Task<(bool Success, string? Error)> RegisterAsync(string username, string password)
        {
            var user = new ApplicationUser
            {
                UserName = username,
                FullName = username
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var error = string.Join("; ", result.Errors.Select(e => e.Description));
                return (false, error);
            }

            return (true, null);
        }

        public async Task LogoutAsync()
        {
            await _signInManager.SignOutAsync();
        }

        public int? GetCurrentUserId()
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(claim, out var userId))
            {
                return userId;
            }

            return null;
        }

        public string? GetCurrentUserName()
        {
            return _httpContextAccessor.HttpContext?.User.Identity?.Name;
        }

        public bool IsAuthenticated()
        {
            return _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
        }
    }
}
