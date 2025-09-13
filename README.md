📘 Hướng dẫn sử dụng ứng dụng (Code First với EF Core)
1. Yêu cầu hệ thống

Hệ điều hành: Windows 10/11

Phần mềm:

Visual Studio Code

SQL Server Management Studio (SSMS)

.NET 8.0 SDK

Cơ sở dữ liệu: SQL Server

2. Cài đặt và cấu hình
2.1. Chuẩn bị database (Code First)

Ứng dụng dùng Code First, nên bạn không cần tạo database thủ công trong SSMS.
Cơ sở dữ liệu sẽ được tự động tạo sau khi chạy migration.

Kiểm tra chuỗi kết nối trong file appsettings.json:

"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=QuanLyChiTieu;User Id=sa;Password=your_password;TrustServerCertificate=True;"
}


🔹 Nếu bạn dùng Windows Authentication thì thay bằng:

"DefaultConnection": "Server=localhost;Database=QuanLyChiTieu;Trusted_Connection=True;TrustServerCertificate=True;"

2.2. Tạo database từ Code First

Mở Terminal trong VSCode, chạy lệnh:

dotnet ef migrations add InitialCreate
dotnet ef database update


migrations add InitialCreate: tạo migration đầu tiên từ các lớp model.

database update: sinh database và các bảng trong SQL Server.

Sau đó mở SSMS để kiểm tra database đã được tạo.

3. Chạy ứng dụng

Chạy bằng lệnh:

dotnet run


Ứng dụng sẽ kết nối với SQL Server và hoạt động trên database vừa tạo.

4. Các chức năng chính

Đăng nhập / Đăng ký người dùng

Quản lý chi tiêu: thêm, sửa, xóa giao dịch

Quản lý ngân sách: đặt hạn mức hàng tháng

Thống kê chi tiêu: biểu đồ theo loại, theo ngày/tháng
