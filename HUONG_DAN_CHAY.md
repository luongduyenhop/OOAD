# Hướng Dẫn Chạy Dự Án SchedulingApp

## 1. Yêu cầu môi trường

- .NET SDK 8.0+
- SQL Server (LocalDB/SQL Server Express/SQL Server thường)
- Công cụ chạy SQL: SSMS hoặc `sqlcmd`

## 2. Chuẩn bị cơ sở dữ liệu

Chạy file SQL đầy đủ:

- File: `database.sql`
- Đường dẫn: `dotnet_app/database.sql`

### Cách 1: dùng SSMS

1. Mở SSMS, kết nối SQL Server local.
2. Mở file `database.sql`.
3. Execute toàn bộ script.

### Cách 2: dùng sqlcmd

```bash
sqlcmd -S localhost -i database.sql
```

Nếu SQL Server của bạn dùng instance khác, đổi `-S` cho phù hợp.

## 3. Kiểm tra cấu hình kết nối

Mở `appsettings.json`, kiểm tra:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=SchedulingApp;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;"
}
```

Nếu server khác localhost, sửa lại `Server=...`.

## 4. Build và chạy ứng dụng

Trong thư mục `dotnet_app`:

```bash
dotnet restore
dotnet build
dotnet run
```

App sẽ hiện URL dạng:
- `https://localhost:xxxxx`
- `http://localhost:yyyyy`

## 5. Đăng nhập

- Tài khoản mặc định: `admin`
- Mật khẩu: `123`

Lưu ý: tài khoản `admin` được seed tự động khi app chạy nếu chưa tồn tại.

## 6. Lỗi thường gặp

### Lỗi khóa file `SchedulingApp.exe` khi build

Nguyên nhân: app đang chạy nền.

Cách xử lý:

```cmd
taskkill /IM SchedulingApp.exe /F
```

Sau đó build lại:

```bash
dotnet build
```

### Lỗi DB cũ không tương thích Identity

Bạn chỉ cần chạy lại `database.sql` mới nhất để đồng bộ schema.

## 7. Ghi chú

- Tên tài khoản hiện dùng theo tên người dùng (`username`).
- Dự án đã bật đăng nhập Identity + cookie auth.
