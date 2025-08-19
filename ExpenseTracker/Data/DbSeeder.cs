using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity; // Thêm using này

namespace ExpenseTracker.Data
{
    public static class DbSeeder
    {
        /// <summary>
        /// Tạo user demo + dữ liệu mẫu (categories, budgets, transactions) nếu chưa có.
        /// Idempotent: chạy nhiều lần cũng không nhân đôi.
        /// </summary>
        public static async Task SeedDemoAsync(AppDbContext db)
        {
            // 1) Tìm hoặc tạo user demo
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == "demo");
            if (user == null)
            {
                var passwordHasher = new PasswordHasher<User>();
                user = new User
                {
                    Username = "demo",
                    Email = "demo@example.com",
                    PasswordHash = passwordHasher.HashPassword(null, "demo123")
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            // 2) Seed Categories cho user demo (nếu chưa có)
            var catNames = new[] { "Ăn uống", "Di chuyển", "Mua sắm", "Hóa đơn & dịch vụ", "Khác" };
            var hasAnyCat = await db.Categories.AnyAsync(c => c.UserId == user.UserId);
            if (!hasAnyCat)
            {
                db.Categories.AddRange(catNames.Select(n => new Category
                {
                    UserId = user.UserId,
                    Name = n,
                    Type = "expense"
                }));
                await db.SaveChangesAsync();
            }

            // Lấy lại map category (để gán giao dịch)
            var cats = await db.Categories
                .Where(c => c.UserId == user.UserId)
                .ToListAsync();

            // 3) Seed Budgets cho 3 tháng gần đây (chỉ ngân sách tổng)
            //    Không tạo trùng nếu đã có.
            for (int i = 0; i < 3; i++)
            {
                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var existsBudget = await db.Budgets.AnyAsync(b =>
                    b.UserId == user.UserId &&
                    b.CategoryId == null &&
                    b.StartDate <= monthEnd && b.EndDate >= monthStart);

                if (!existsBudget)
                {
                    db.Budgets.Add(new Budget
                    {
                        UserId = user.UserId,
                        CategoryId = null, // tổng
                        Amount = 20_000_000m,
                        StartDate = monthStart,
                        EndDate = monthEnd
                    });
                }
            }
            await db.SaveChangesAsync();

            // 4) Seed Transactions rải đều 2–3 tháng gần đây (nếu chưa có giao dịch nào)
            var hasAnyTx = await db.Transactions.AnyAsync(t => t.UserId == user.UserId);
            if (!hasAnyTx)
            {
                var rnd = new Random();
                var start = DateTime.Today.AddMonths(-3);
                var txs = new List<Transaction>();

                for (int i = 0; i < 120; i++)
                {
                    var day = start.AddDays(rnd.Next(0, 90));
                    var cat = cats[rnd.Next(cats.Count)];
                    txs.Add(new Transaction
                    {
                        UserId = user.UserId,
                        CategoryId = cat.CategoryId,
                        Amount = rnd.Next(20_000, 1_500_000),
                        TransactionDate = day,
                        Note = $"Giao dịch mẫu #{i + 1}"
                    });
                }

                db.Transactions.AddRange(txs);
                await db.SaveChangesAsync();
            }
        }
    }
}
