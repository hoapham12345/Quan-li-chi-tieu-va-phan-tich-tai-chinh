using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;          // <- PasswordHasher
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Data;
using ExpenseTracker.Models;

namespace ExpenseTracker.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _hasher;

        public AuthController(AppDbContext db)
        {
            _db = db;
            _hasher = new PasswordHasher<User>();
        }

        // ======================
        // REGISTER
        // ======================
        [HttpGet, AllowAnonymous]
        public IActionResult Register() => View();

        [HttpPost, AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string username, string email, string password)
        {
            username = (username ?? "").Trim();
            email = (email ?? "").Trim();
            password = (password ?? "").Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập tên đăng nhập và mật khẩu.");
                return View();
            }

            var existed = await _db.Users.AnyAsync(u => u.Username == username);
            if (existed)
            {
                ModelState.AddModelError("", "Tên đăng nhập đã tồn tại.");
                return View();
            }

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = "" // sẽ set bên dưới
            };

            // Băm mật khẩu
            user.PasswordHash = _hasher.HashPassword(user, password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Đăng nhập luôn sau khi đăng ký
            await SignInUser(user);

            return RedirectToAction("Index", "Dashboard");
        }

        // ======================
        // LOGIN / LOGOUT
        // ======================
        [HttpGet, AllowAnonymous]
        public IActionResult Login() => View();

        [HttpPost, AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            username = (username ?? "").Trim();
            password = (password ?? "").Trim();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                ModelState.AddModelError("", "Sai tên đăng nhập hoặc mật khẩu.");
                return View();
            }

            var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verify == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Sai tên đăng nhập hoặc mật khẩu.");
                return View();
            }

            await SignInUser(user);
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ======================
        // CHANGE PASSWORD
        // ======================
        [HttpGet, Authorize]
        public IActionResult ChangePassword() => View();

        [HttpPost, Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) ||
                string.IsNullOrWhiteSpace(newPassword) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                ModelState.AddModelError("", "Vui lòng nhập đủ các trường.");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu mới và xác nhận không khớp.");
                return View();
            }

            // Lấy user hiện tại từ claims
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out var uid))
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == uid);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            // Kiểm tra mật khẩu hiện tại
            var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
            if (verify == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Mật khẩu hiện tại không đúng.");
                return View();
            }

            // Đổi mật khẩu
            user.PasswordHash = _hasher.HashPassword(user, newPassword);
            await _db.SaveChangesAsync();

            TempData["msg"] = "Đổi mật khẩu thành công.";
            return RedirectToAction("Index", "Dashboard");
        }

        // ======================
        // Helpers
        // ======================
        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }
    }
}
