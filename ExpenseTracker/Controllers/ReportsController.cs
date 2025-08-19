using ExpenseTracker.Data;
using ExpenseTracker.Services; // nếu muốn hiện thêm Insights
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly FinancialAnalysisService? _analysis; // optional
        private int CurrentUserId
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        public ReportsController(AppDbContext db, FinancialAnalysisService? analysis = null)
        {
            _db = db;
            _analysis = analysis;
        }

        public async Task<IActionResult> Index(DateTime? start, DateTime? end)
        {
            var userId = CurrentUserId;

            // mặc định: tháng hiện tại
            var today = DateTime.Today;
            var from = (start ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (end ?? from.AddMonths(1).AddDays(-1)).Date;

            // truy vấn cơ sở
            var baseQuery = _db.Transactions.AsNoTracking()
                .Where(t => t.UserId == userId &&
                            t.TransactionDate >= from &&
                            t.TransactionDate <= to)
                .Include(t => t.Category);

            var totalSpent = await baseQuery.SumAsync(t => (decimal?)t.Amount) ?? 0m;

            // ngân sách kỳ hiện tại (nếu có)
            var budget = await _db.Budgets.AsNoTracking()
                .Where(b => b.UserId == userId && b.StartDate <= to && b.EndDate >= from)
                .Select(b => (decimal?)b.Amount).FirstOrDefaultAsync() ?? 0m;

            // chi theo danh mục
            var byCategory = await baseQuery
                .GroupBy(t => t.Category != null ? t.Category.Name : "Khác")
                .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Total)
                .ToListAsync();

            // chi theo ngày (xu hướng)
            var byDay = await baseQuery
                .GroupBy(t => t.TransactionDate.Date)
                .Select(g => new { Day = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderBy(x => x.Day)
                .ToListAsync();

            // so sánh với kỳ trước (cùng độ dài)
            var prevFrom = from.AddMonths(-1);
            var prevTo = to.AddMonths(-1);
            var prevSpent = await _db.Transactions.AsNoTracking()
                .Where(t => t.UserId == userId &&
                            t.TransactionDate >= prevFrom &&
                            t.TransactionDate <= prevTo)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            var delta = totalSpent - prevSpent;
            var pctChange = prevSpent == 0 ? (decimal?)null : (totalSpent - prevSpent) / prevSpent;

            // top danh mục & top giao dịch
            var topCategories = byCategory.Take(5).ToList();
            var topTransactions = await baseQuery
                .OrderByDescending(t => t.Amount)
                .Take(5)
                .ToListAsync();

            // optional: insights nâng cao
            var insights = _analysis != null
                ? await _analysis.GetMonthlyInsights(userId, from, to)
                : new List<ExpenseTracker.Models.Insight>();

            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.TotalSpent = totalSpent;
            ViewBag.Budget = budget;
            ViewBag.PrevSpent = prevSpent;
            ViewBag.Delta = delta;
            ViewBag.PctChange = pctChange;
            ViewBag.ByCategory = byCategory;
            ViewBag.ByDay = byDay;
            ViewBag.TopCategories = topCategories;
            ViewBag.TopTransactions = topTransactions;
            ViewBag.Insights = insights;

            return View();
        }

        // Xuất CSV nhanh
        [HttpGet]
        public async Task<IActionResult> ExportCsv(DateTime? start, DateTime? end)
        {
            var userId = CurrentUserId;
            var today = DateTime.Today;
            var from = (start ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (end ?? from.AddMonths(1).AddDays(-1)).Date;

            var rows = await _db.Transactions.AsNoTracking()
                .Where(t => t.UserId == userId && t.TransactionDate >= from && t.TransactionDate <= to)
                .Include(t => t.Category)
                .OrderBy(t => t.TransactionDate)
                .Select(t => new {
                    Date = t.TransactionDate.ToString("yyyy-MM-dd"),
                    Category = t.Category != null ? t.Category.Name : "Khác",
                    t.Amount,
                    t.Note
                }).ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Date,Category,Amount,Note");
            foreach (var r in rows)
                sb.AppendLine($"{r.Date},{r.Category},{r.Amount},{(r.Note ?? "").Replace(",", " ")}");

            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "report.csv");
        }
    }
}
