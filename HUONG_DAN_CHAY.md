# Huong Dan Cai Dat va Cau Hinh Du An SchedulingApp

Tai lieu nay huong dan cai dat, cau hinh va chay demo web "Smart Scheduler".

## 1. Yeu cau moi truong

- .NET SDK 8.0+
- SQL Server (LocalDB / SQL Server Express / SQL Server)
- Cong cu quan ly DB: SSMS hoac `sqlcmd`
- (Tuy chon) Tai khoan SMTP de gui email reminder (Gmail, Brevo, Mailgun, ...)

## 2. Cau truc thu muc

- Source web app: `dotnet_app/`
- Schema SQL (Identity + business tables): `dotnet_app/database.sql`
- Cau hinh: `dotnet_app/appsettings.json`

## 3. Chuan bi co so du lieu

Khuyen nghi dung script SQL de dong bo schema mot lan truoc khi chay:

- File: `dotnet_app/database.sql`

### Cach 1: SSMS

1. Mo SSMS, ket noi SQL Server.
2. Mo `dotnet_app/database.sql`.
3. Execute toan bo script.

### Cach 2: sqlcmd

Chay trong thu muc `dotnet_app`:

```bash
sqlcmd -S localhost -i database.sql
```

Neu SQL Server dung instance khac, doi `-S` cho phu hop.

Ghi chu:
- Khi app khoi dong, app co co che tu bo sung schema cho DB cu (vi du: them cot `Tasks.Priority`, tao bang `ReminderNotifications` neu chua co).

## 4. Cau hinh ket noi DB

Mo `dotnet_app/appsettings.json` va sua `DefaultConnection`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=SchedulingApp;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;"
}
```

Neu dung SQL Authentication (user/pass) thi can doi connection string theo moi truong cua ban.

## 5. Build va chay ung dung

Trong thu muc `dotnet_app`:

```bash
dotnet restore
dotnet build
dotnet run
```

App se in ra URL dang:
- `https://localhost:xxxxx`
- `http://localhost:yyyyy`

Moi truong mac dinh khi chay bang launchSettings la `Development`.

## 6. Tai khoan demo

Trong moi truong `Development`, app se seed tai khoan mau:
- Email: `admin@scheduler.local`
- Mat khau: `Admin1234`

Luu y:
- Tai khoan mau chi de dev/demo, khong phai email that.
- Muon nhan email reminder, hay dang ky tai khoan bang email that (vi du Gmail cua ban).

## 7. Reminder in-app

- Reminder engine chay nen moi ~30 giay.
- Khi task den gio nhac, he thong tao `ReminderNotification` (in-app).
- Tren trang Tasks co danh sach nhac viec chua doc + nut danh dau da doc.

## 8. Bat gui Email Reminder (SMTP)

### 8.1 Cau hinh trong appsettings.json

Mo `dotnet_app/appsettings.json` va cau hinh `Smtp`:

```json
"Smtp": {
  "Enabled": true,
  "Host": "smtp.gmail.com",
  "Port": 587,
  "UseSsl": true,
  "UserName": "your_account@gmail.com",
  "Password": "your_app_password",
  "FromEmail": "your_account@gmail.com",
  "FromName": "Smart Scheduler"
}
```

### 8.2 Khuyen nghi: dung user-secrets (khong commit password)

Trong `dotnet_app`:

```bash
dotnet user-secrets init
dotnet user-secrets set "Smtp:Enabled" "true"
dotnet user-secrets set "Smtp:Host" "smtp.gmail.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:UseSsl" "true"
dotnet user-secrets set "Smtp:UserName" "your_account@gmail.com"
dotnet user-secrets set "Smtp:Password" "your_app_password"
dotnet user-secrets set "Smtp:FromEmail" "your_account@gmail.com"
dotnet user-secrets set "Smtp:FromName" "Smart Scheduler"
```

### 8.3 Test gui email

1. Dang nhap bang tai khoan co email that.
2. Vao man hinh Tasks.
3. Bam nut `Gui email test` (sidebar).
4. Kiem tra Inbox va Spam.

Ghi chu Gmail:
- Can bat 2FA va dung App Password (khong dung mat khau dang nhap thuong).

### 8.4 Tao Gmail App Password

1. Vao Google Account -> Security.
2. Bat 2-Step Verification (Xac minh 2 buoc).
3. Trong Security, tim "App passwords" (Mat khau ung dung).
4. Tao app password cho "Mail" (hoac "Other"), dat ten de nho.
5. Copy chuoi 16 ky tu (co the hien thanh 4 nhom). Khi dan vao cau hinh, nen bo het dau cach.
6. Gan vao `Smtp:Password` va restart app.

Neu khong thay muc App passwords: kiem tra da bat 2FA chua, hoac tai khoan bi gioi han (work/school/child).
Khi do co the dung SMTP provider khac (Brevo, Mailgun, SendGrid, ...).

## 9. Loi thuong gap

### 9.1 Build bi khoa file (locked)

Neu `SchedulingApp.exe` / `SchedulingApp.dll` dang bi khoa, tat process dang chay roi build lai.

Tren Windows (cmd):

```cmd
taskkill /IM SchedulingApp.exe /F
```

### 9.2 SMTP khong gui duoc

- Kiem tra `Smtp.Enabled=true`, `Smtp.Host` va `Smtp.FromEmail` khong rong.
- Dam bao tai khoan dang nhap co `Email` that.
- Kiem tra firewall/network chan port 587/465.
- Gmail: dung App Password, va thu bo dau cach trong password neu copy bi chen space.
