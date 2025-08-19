using ExpenseTracker.Data;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _db;
        private readonly FinancialAnalysisService _analysis;
        public DashboardController(AppDbContext db, FinancialAnalysisService analysis)
        {
            _db = db;
            _analysis = analysis;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;

        public async Task<IActionResult> Index()
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            var totalThisMonth = await _db.Transactions.AsNoTracking()
                .Where(t => t.UserId == uid &&
                            t.TransactionDate >= startOfMonth &&
                            t.TransactionDate <= today)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            var budgetAmount = await _db.Budgets.AsNoTracking()
                .Where(b => b.UserId == uid &&
                            b.StartDate <= today && b.EndDate >= today)
                .Select(b => (decimal?)b.Amount)
                .FirstOrDefaultAsync() ?? 0m;

            var byCategory = await _db.Transactions.AsNoTracking()
                .Where(t => t.UserId == uid &&
                            t.TransactionDate >= startOfMonth &&
                            t.TransactionDate <= today)
                .GroupBy(t => t.Category != null ? t.Category.Name : "Khác")
                .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Total)
                .ToListAsync();

            var trend = await _db.Transactions.AsNoTracking()
                .Where(t => t.UserId == uid)
                .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Total = g.Sum(x => x.Amount) })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            var recent = await _db.Transactions.AsNoTracking()
                .Where(t => t.UserId == uid)
                .Include(t => t.Category)
                .OrderByDescending(t => t.TransactionDate)
                .Take(10)
                .ToListAsync();

            var insights = await _analysis.GetMonthlyInsights(uid.Value, startOfMonth, today);

            var percentUsed = budgetAmount == 0
                ? 0.0
                : Math.Min(100.0, (double)(totalThisMonth / budgetAmount * 100m));

            ViewBag.TotalThisMonth = totalThisMonth;
            ViewBag.BudgetAmount = budgetAmount;
            ViewBag.ByCategory = byCategory;
            ViewBag.Trend = trend;
            ViewBag.Recent = recent;
            ViewBag.Insights = insights;
            ViewBag.PercentUsed = percentUsed;

            return View();
        }
    }
}
