using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
        public DbSet<Budget> Budgets { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- User ----
            modelBuilder.Entity<User>(e =>
            {
                // Nếu bạn muốn unique
                e.HasIndex(x => x.Username).IsUnique();
                e.HasIndex(x => x.Email).IsUnique();

                // Mặc định SQL Server: tự điền CreatedAt khi insert
                e.Property(x => x.CreatedAt)
                 .HasDefaultValueSql("GETUTCDATE()");
            });

            // (tuỳ chọn) cấu hình thêm các entity khác nếu cần
        }
    }
}
