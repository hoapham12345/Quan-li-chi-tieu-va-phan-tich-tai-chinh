using ExpenseTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<FinancialAnalysisService>();
builder.Services.AddDataProtection()
    .SetApplicationName("ExpenseTrackerDev");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.Cookie.Name = "ExpenseTracker.Auth";   // tên cookie riêng, tránh đụng các app khác
        opt.LoginPath = "/Auth/Login";
        opt.LogoutPath = "/Auth/Logout";
        opt.AccessDeniedPath = "/Auth/Login";
        opt.SlidingExpiration = true;
        opt.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ExpenseTracker.Services.ICurrentUser, ExpenseTracker.Services.CurrentUser>();



// MVC + Views
builder.Services.AddControllersWithViews();

// EF Core (đổi sang UseSqlite nếu muốn)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Swagger (tuỳ chọn)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Expense Tracker API", Version = "v1" });
});

var app = builder.Build();

///// ----- SEEDING: tạo DB + bơm dữ liệu mẫu -----
//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

//    db.Database.EnsureCreated();


//}
///// -------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Tracker API v1");
        c.RoutePrefix = "swagger"; // Swagger ở /swagger
    });
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedDemoAsync(db);
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Route MVC mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
