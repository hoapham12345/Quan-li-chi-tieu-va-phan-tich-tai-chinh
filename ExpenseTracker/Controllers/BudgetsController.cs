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
    public class BudgetsController : Controller
    {
        private readonly AppDbContext _db;
        public BudgetsController(AppDbContext db) => _db = db;

        // Lấy UserId từ claim
        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;

        // GET: /Budgets?month=&year=
        public async Task<IActionResult> Index(int? month, int? year)
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            var today = DateTime.Today;
            var y = year ?? today.Year;
            var m = month ?? today.Month;

            var periodStart = new DateTime(y, m, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            // Ngân sách trong kỳ (tổng + theo danh mục)
            var budgets = await _db.Budgets.AsNoTracking()
                .Where(b => b.UserId == uid &&
                            b.StartDate <= periodEnd && b.EndDate >= periodStart)
                .Include(b => b.Category)
                .OrderBy(b => b.CategoryId == null)
                .ThenBy(b => b.Category != null ? b.Category.Name : "")
                .ToListAsync();

            // Chi trong kỳ (group theo CategoryId)
            var spentList = await _db.Transactions.AsNoTracking()
                .Where(t => t.UserId == uid &&
                            t.TransactionDate >= periodStart &&
                            t.TransactionDate <= periodEnd)
                .GroupBy(t => t.CategoryId)
                .Select(g => new { CategoryId = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            decimal totalSpent = spentList.Sum(x => x.Total);

            var spentMap = spentList
                .Where(x => x.CategoryId.HasValue)
                .ToDictionary(x => x.CategoryId!.Value, x => x.Total);

            var totalBudget = budgets.FirstOrDefault(b => b.CategoryId == null)?.Amount ?? 0m;
            ViewBag.PercentUsed = totalBudget == 0
                ? 0.0
                : Math.Min(100.0, (double)(totalSpent / totalBudget * 100m));

            ViewBag.Month = m;
            ViewBag.Year = y;
            ViewBag.PeriodStart = periodStart;
            ViewBag.PeriodEnd = periodEnd;
            ViewBag.SpentMap = spentMap;
            ViewBag.TotalSpent = totalSpent;

            // Lịch sử 6 tháng (nhãn + % dùng ngân sách)
            var last6 = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var mStart = new DateTime(y, m, 1).AddMonths(-i);
                var mEnd = mStart.AddMonths(1).AddDays(-1);

                var spentM = await _db.Transactions.AsNoTracking()
                    .Where(t => t.UserId == uid &&
                                t.TransactionDate >= mStart &&
                                t.TransactionDate <= mEnd)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;

                var budgetM = await _db.Budgets.AsNoTracking()
                    .Where(b => b.UserId == uid &&
                                b.CategoryId == null &&
                                b.StartDate <= mEnd && b.EndDate >= mStart)
                    .SumAsync(b => (decimal?)b.Amount) ?? 0m;

                double pct = budgetM == 0 ? 0.0 : (double)(spentM / budgetM * 100m);

                last6.Add(new { Label = $"{mStart.Month}/{mStart.Year}", Percent = Math.Round(pct, 1) });
            }
            ViewBag.Last6 = last6;

            // Gợi ý ngân sách kỳ sau (trung bình 3 tháng gần nhất, làm tròn 100k)
            var last3Spent = new List<decimal>();
            for (int i = 1; i <= 3; i++)
            {
                var s = periodStart.AddMonths(-i);
                var e = s.AddMonths(1).AddDays(-1);
                var v = await _db.Transactions.AsNoTracking()
                    .Where(t => t.UserId == uid && t.TransactionDate >= s && t.TransactionDate <= e)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;
                last3Spent.Add(v);
            }
            var avg3 = last3Spent.Count > 0 ? last3Spent.Average() : 0m;
            decimal suggestNext = avg3 <= 0 ? 0 : Math.Ceiling(avg3 / 100000m) * 100000m;

            var nextStart = periodStart.AddMonths(1);
            var nextEnd = periodEnd.AddMonths(1);

            bool nextHasBudget = await _db.Budgets.AnyAsync(b => b.UserId == uid &&
                                                                 b.CategoryId == null &&
                                                                 b.StartDate <= nextEnd &&
                                                                 b.EndDate >= nextStart);

            ViewBag.SuggestNextAmount = suggestNext;
            ViewBag.SuggestNextStart = nextStart;
            ViewBag.SuggestNextEnd = nextEnd;
            ViewBag.NextHasBudget = nextHasBudget;

            return View(budgets);
        }

        // POST: /Budgets/ApplySuggestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplySuggestion(DateTime start, DateTime end, decimal amount)
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            bool exists = await _db.Budgets.AnyAsync(b => b.UserId == uid &&
                                                          b.CategoryId == null &&
                                                          b.StartDate <= end && b.EndDate >= start);
            if (!exists)
            {
                _db.Budgets.Add(new Budget
                {
                    UserId = uid.Value,
                    CategoryId = null,
                    Amount = amount,
                    StartDate = start.Date,
                    EndDate = end.Date
                });
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { month = start.Month, year = start.Year });
        }

        // GET: /Budgets/Create
        public async Task<IActionResult> Create()
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            await LoadCategories(uid.Value);
            var today = DateTime.Today;
            var model = new Budget
            {
                UserId = uid.Value,
                StartDate = new DateTime(today.Year, today.Month, 1),
                EndDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))
            };
            return View(model);
        }

        // POST: /Budgets/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Budget model)
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            if (model.StartDate > model.EndDate)
                ModelState.AddModelError("", "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");

            if (!ModelState.IsValid)
            {
                await LoadCategories(uid.Value);
                return View(model);
            }

            model.UserId = uid.Value;
            _db.Budgets.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { month = model.StartDate.Month, year = model.StartDate.Year });
        }

        // GET: /Budgets/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == id && b.UserId == uid);
            if (budget == null) return NotFound();

            await LoadCategories(uid.Value);
            return View(budget);
        }

        // POST: /Budgets/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Budget model)
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            if (id != model.BudgetId) return NotFound();

            if (model.StartDate > model.EndDate)
                ModelState.AddModelError("", "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");

            if (!ModelState.IsValid)
            {
                await LoadCategories(uid.Value);
                return View(model);
            }

            var dbBudget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == id && b.UserId == uid);
            if (dbBudget == null) return NotFound();

            dbBudget.CategoryId = model.CategoryId;
            dbBudget.Amount = model.Amount;
            dbBudget.StartDate = model.StartDate;
            dbBudget.EndDate = model.EndDate;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { month = model.StartDate.Month, year = model.StartDate.Year });
        }

        // POST: /Budgets/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            var b = await _db.Budgets.FirstOrDefaultAsync(x => x.BudgetId == id && x.UserId == uid);
            if (b != null)
            {
                _db.Budgets.Remove(b);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: /Budgets/Clone
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clone(int fromMonth, int fromYear, int toMonth, int toYear)
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Auth");

            var fromStart = new DateTime(fromYear, fromMonth, 1);
            var fromEnd = fromStart.AddMonths(1).AddDays(-1);

            var toStart = new DateTime(toYear, toMonth, 1);
            var toEnd = toStart.AddMonths(1).AddDays(-1);

            var src = await _db.Budgets
                .Where(b => b.UserId == uid &&
                            b.StartDate <= fromEnd && b.EndDate >= fromStart)
                .ToListAsync();

            if (!src.Any())
                return RedirectToAction(nameof(Index), new { month = toMonth, year = toYear });

            foreach (var b in src)
            {
                _db.Budgets.Add(new Budget
                {
                    UserId = uid.Value,
                    CategoryId = b.CategoryId,
                    Amount = b.Amount,
                    StartDate = toStart,
                    EndDate = toEnd
                });
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { month = toMonth, year = toYear });
        }

        private async Task LoadCategories(int userId)
        {
            var cats = await _db.Categories.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = new SelectList(cats, "CategoryId", "Name");
        }
    }
}
