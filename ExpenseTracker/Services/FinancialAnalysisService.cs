// Services/FinancialAnalysisService.cs
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class FinancialAnalysisService
    {
        private readonly AppDbContext _db;
        public FinancialAnalysisService(AppDbContext db) { _db = db; }

        // Tham số ngưỡng có thể chỉnh trong config
        private const decimal WarnBudgetRatio = 0.9m;   // >=90% ngân sách -> cảnh báo vàng
        private const decimal DangerBudgetRatio = 1.0m; // >100% ngân sách -> cảnh báo đỏ
        private const int AnomalyZScore = 2;            // z-score > 2 coi là bất thường

        public async Task<List<Insight>> GetMonthlyInsights(int userId, DateTime from, DateTime to)
        {
            var list = new List<Insight>();

            // 1) Dự báo cuối tháng (forecast)
            var fc = await ForecastSpending(userId, from, to);
            if (fc != null)
            {
                var type = fc.Value <= (await GetBudgetAmount(userId, to)) ? "info" : "warn";
                list.Add(new Insight
                {
                    Type = type,
                    Title = "Dự báo chi tiêu cuối kỳ",
                    Detail = $"{fc.Value:N0} đ trong kỳ hiện tại"
                });
            }

            // 2) Vượt ngân sách tổng
            var budget = await GetBudgetAmount(userId, to);
            var spent = await _db.Transactions
                .Where(t => t.UserId == userId && t.TransactionDate >= from && t.TransactionDate <= to)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            if (budget > 0)
            {
                var ratio = spent / budget;
                if (ratio >= DangerBudgetRatio)
                    list.Add(new Insight
                    {
                        Type = "danger",
                        Title = "Vượt ngân sách tổng",
                        Detail = $"{spent:N0} / {budget:N0} đ",
                        Percent = ratio
                    });
                else if (ratio >= WarnBudgetRatio)
                    list.Add(new Insight
                    {
                        Type = "warn",
                        Title = "Tiệm cận ngân sách tổng",
                        Detail = $"{spent:N0} / {budget:N0} đ (≥90%)",
                        Percent = ratio
                    });
            }

            // 3) Cảnh báo theo danh mục (nếu có ngân sách danh mục)
            list.AddRange(await CategoryBudgetAlerts(userId, from, to));

            // 4) Bất thường theo ngày (spike)
            var spike = await DetectDailyAnomaly(userId, from, to);
            if (spike != null)
            {
                list.Add(new Insight
                {
                    Type = "warn",
                    Title = $"Chi tiêu bất thường ngày {spike.Value.day:dd/MM}",
                    Detail = $"+{spike.Value.delta:N0} đ so với trung bình ngày"
                });
            }

            // 5) Xu hướng tăng mạnh so với trung bình 3 tháng
            var mo = await MonthOverMonthChange(userId, to);
            if (mo != null && mo.Value.pctChange >= 0.2m)
            {
                list.Add(new Insight
                {
                    Type = "info",
                    Title = "Xu hướng tăng chi",
                    Detail = $"+{mo.Value.pctChange:P0} so với trung bình 3 tháng gần"
                });
            }

            return list;
        }

        // ---- Các hàm con (đơn giản, dễ hiểu) ----

        // Dự báo = chi trung bình/ngày * số ngày trong kỳ
        private async Task<decimal?> ForecastSpending(int userId, DateTime from, DateTime to)
        {
            var today = DateTime.Today;
            var end = to.Date;
            var start = from.Date;
            var daysPassed = Math.Max(1, (today < start ? 1 : (today - start).Days + 1));
            var daysInPeriod = (end - start).Days + 1;

            var spentSoFar = await _db.Transactions
                .Where(t => t.UserId == userId && t.TransactionDate >= start && t.TransactionDate <= (today < end ? today : end))
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            var perDay = spentSoFar / daysPassed;
            return Math.Round(perDay * daysInPeriod, 0);
        }

        private async Task<decimal> GetBudgetAmount(int userId, DateTime anyDayInPeriod)
        {
            var b = await _db.Budgets.FirstOrDefaultAsync(x => x.UserId == userId && x.StartDate <= anyDayInPeriod && x.EndDate >= anyDayInPeriod);
            return b?.Amount ?? 0m;
        }

        // Cảnh báo danh mục nếu bạn có bảng ngân sách theo danh mục (tuỳ chọn)
        private async Task<List<Insight>> CategoryBudgetAlerts(int userId, DateTime from, DateTime to)
        {
            var list = new List<Insight>();
            // Nếu chưa có ngân sách theo danh mục, có thể bỏ qua khối này
            // Ở đây ví dụ: dùng Budget.CategoryId != null coi như ngân sách theo danh mục
            var catBudgets = await _db.Budgets
                .Where(b => b.UserId == userId && b.CategoryId != null && b.StartDate <= to && b.EndDate >= from)
                .ToListAsync();

            if (!catBudgets.Any()) return list;

            var spentByCat = await _db.Transactions
                .Where(t => t.UserId == userId && t.TransactionDate >= from && t.TransactionDate <= to)
                .GroupBy(t => t.CategoryId)
                .Select(g => new { CategoryId = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            foreach (var b in catBudgets)
            {
                var spent = spentByCat.FirstOrDefault(x => x.CategoryId == b.CategoryId)?.Total ?? 0m;
                if (b.Amount == 0) continue;

                var ratio = spent / b.Amount;
                if (ratio >= DangerBudgetRatio || ratio >= WarnBudgetRatio)
                {
                    var catName = await _db.Categories.Where(c => c.CategoryId == b.CategoryId).Select(c => c.Name).FirstOrDefaultAsync() ?? "Danh mục";
                    list.Add(new Insight
                    {
                        Type = ratio >= DangerBudgetRatio ? "danger" : "warn",
                        Title = $"{catName}: {(ratio >= DangerBudgetRatio ? "vượt" : "tiệm cận")} ngân sách",
                        Detail = $"{spent:N0} / {b.Amount:N0} đ",
                        Percent = ratio
                    });
                }
            }
            return list;
        }

        // Bất thường theo ngày: z-score > 2
        private async Task<(DateTime day, decimal delta)?> DetectDailyAnomaly(int userId, DateTime from, DateTime to)
        {
            var rows = await _db.Transactions
                .Where(t => t.UserId == userId && t.TransactionDate >= from && t.TransactionDate <= to)
                .GroupBy(t => t.TransactionDate.Date)
                .Select(g => new { Day = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderBy(x => x.Day)
                .ToListAsync();

            if (rows.Count < 7) return null; // dữ liệu quá ít

            var mean = rows.Average(x => x.Total);
            var variance = rows.Average(x => (x.Total - mean) * (x.Total - mean));
            var std = (decimal)Math.Sqrt((double)variance);
            if (std == 0) return null;

            // tìm ngày có z-score lớn nhất
            var top = rows
                .Select(x => new { x.Day, x.Total, z = (x.Total - mean) / std })
                .OrderByDescending(x => x.z)
                .First();

            if (top.z > AnomalyZScore)
                return (top.Day, top.Total - mean);

            return null;
        }

        // So sánh tổng chi tháng hiện tại vs. trung bình 3 tháng trước
        private async Task<(decimal pctChange, decimal current, decimal avgPrev3)?> MonthOverMonthChange(int userId, DateTime anyDayInCurrentMonth)
        {
            var y = anyDayInCurrentMonth.Year;
            var m = anyDayInCurrentMonth.Month;

            // tổng chi tháng hiện tại
            var startCur = new DateTime(y, m, 1);
            var endCur = startCur.AddMonths(1).AddDays(-1);
            var cur = await _db.Transactions
                .Where(t => t.UserId == userId && t.TransactionDate >= startCur && t.TransactionDate <= endCur)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            // trung bình 3 tháng trước
            var prev3 = new List<decimal>();
            for (int i = 1; i <= 3; i++)
            {
                var s = startCur.AddMonths(-i);
                var e = s.AddMonths(1).AddDays(-1);
                var v = await _db.Transactions
                    .Where(t => t.UserId == userId && t.TransactionDate >= s && t.TransactionDate <= e)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;
                prev3.Add(v);
            }
            var avg = prev3.Count > 0 ? prev3.Average() : 0m;
            if (avg == 0) return null;

            var pct = (cur - avg) / avg;
            return (pct, cur, avg);
        }
    }
}
