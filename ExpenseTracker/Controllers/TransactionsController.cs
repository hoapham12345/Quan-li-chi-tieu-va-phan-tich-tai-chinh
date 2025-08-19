using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class TransactionsController : Controller
    {
        private readonly AppDbContext _db;
        public TransactionsController(AppDbContext db) => _db = db;

        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;

        // GET: /Transactions
        public async Task<IActionResult> Index()
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            var tx = await _db.Transactions.AsNoTracking()
                .Where(t => t.UserId == uid)
                .Include(t => t.Category)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return View(tx);
        }

        // GET: /Transactions/Create
        public async Task<IActionResult> Create()
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            await LoadCategoriesForUser(uid.Value);
            return View(new Transaction { TransactionDate = DateTime.Today });
        }

        // POST: /Transactions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CategoryId,Amount,TransactionDate,Note")] Transaction model)
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
            {
                await LoadCategoriesForUser(uid.Value, model.CategoryId);
                return View(model);
            }

            model.UserId = uid.Value;
            _db.Transactions.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Helper: nạp SelectList danh mục
        private async Task LoadCategoriesForUser(int userId, int? selectedId = null)
        {
            var categories = await _db.Categories.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.CategoryId = new SelectList(categories, nameof(Category.CategoryId), nameof(Category.Name), selectedId);
        }
    }
}
