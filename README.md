# ResAuthApi

Hệ thống **Authentication API** 
sử dụng kiến trúc **Clean Architecture** để quản lý xác thực người dùng qua **Azure AD** và cấp phát **JWT Access Token** + **Refresh Token** an toàn.  
Hỗ trợ cơ chế **Refresh Token Rotation**, thu hồi token, và dọn dẹp token hết hạn định kỳ.

---

## 📑 Mục lục
1. [Tính năng](#-tính-năng)
2. [Kiến trúc](#-kiến-trúc)
3. [Cấu trúc thư mục](#-cấu-trúc-thư-mục)
4. [Cài đặt](#-cài-đặt)
5. [Cấu hình](#-cấu-hình)
6. [API Endpoints](#-api-endpoints)
7. [Công nghệ sử dụng](#-công-nghệ-sử-dụng)
8. [Ghi chú bảo mật](#-ghi-chú-bảo-mật)

---

## 🚀 Tính năng
- **Đăng nhập Azure AD** qua OpenID Connect Authorization Code Flow.
- **Phát hành Access Token (JWT)** ký bằng RSA SHA256.
- **Quản lý Refresh Token** với cơ chế:
  - Lưu trữ dưới dạng SHA256 hash.
  - Rotation khi làm mới token.
  - Thu hồi (Revoke) với lý do.
  - Tự động dọn dẹp token hết hạn mỗi giờ.
- **Public key endpoint** cho các service khác verify JWT.
- **Logging** bằng Serilog, lưu file log hàng ngày.
- **CORS** hỗ trợ SPA (React, Vue...) ở `localhost:3000`.
- **Redis** cached để hỗ trợ chạy nhiều instant
- **signalR** push thông báo logout

---

## 🏛 Kiến trúc
Dự án áp dụng **Clean Architecture** gồm 4 tầng:

1. **Domain**  
   - Chứa entity `RefreshToken`.
   - Không phụ thuộc vào framework hay thư viện bên ngoài.

2. **Application**  
   - Khai báo **Interfaces** (`IAzureAdService`, `IRefreshTokenRepository`).
   - DTOs (`RefreshResponse`).
   - Không chứa logic hạ tầng.

3. **Infrastructure**  
   - Implement repository với **Dapper** (`DapperRefreshTokenRepository`).
   - Factory kết nối SQL (`SqlConnectionFactory`).

4. **Api**  
   - Controllers (`AuthController`, `AuthControllerMobile`, `KeysController`).
   - Services (`AzureAdService`, `TokenService`, `RefreshCleanupService`, `LogoutNotifier`).
   - Utils (`TokenHasher`, `KeyLoader`).
   - Program.cs cấu hình DI, JWT, Swagger, Serilog, CORS.

---

## 📂 Cấu trúc thư mục

ResAuthApi.sln
 ├─ ResAuthApi.Api/
 │   ├─ Controllers/
 │   │   ├─ AuthController.cs
 │   │   └─ KeysController.cs
 │   ├─ Services/
 │   │   ├─ AzureAdService.cs
 │   │   ├─ TokenService.cs
 │   │   └─ RefreshCleanupService.cs
 │   ├─ Utils/
 │   │   ├─ KeyLoader.cs
 │   │   └─ TokenHasher.cs
 │   ├─ Program.cs
 │   ├─ appsettings.json
 │   └─ ResAuthApi.Api.csproj
 ├─ ResAuthApi.Application/
 │   ├─ Interfaces/
 │   │   ├─ IRefreshTokenRepository.cs
 │   │   └─ IAzureAdService.cs
 │   ├─ DTOs/
 │   │   └─ AuthDtos.cs
 │   └─ ResAuthApi.Application.csproj
 ├─ ResAuthApi.Domain/
 │   ├─ Entities/
 │   │   └─ RefreshToken.cs
 │   └─ ResAuthApi.Domain.csproj
 └─ ResAuthApi.Infrastructure/
     ├─ Persistence/
     │   └─ DapperRefreshTokenRepository.cs
     ├─ SqlConnectionFactory.cs
     └─ ResAuthApi.Infrastructure.csproj
Ops/
 └─ sql/Init.sql
Keys/
 ├─ private.key     (PKCS#8 PEM, RSA PRIVATE KEY)
 └─ public.key      (SubjectPublicKeyInfo PEM)


## 2. Cài đặt .NET SDK
Yêu cầu **.NET 8.0** trở lên.

## 3. Cấu hình CSDL
- Tạo database **SQL Server**.
- Chạy script trong `ops/sql/init.sql`.

## 4. Cấu hình Azure AD
Lấy các thông tin:
- **TenantId**
- **ClientId**
- **ClientSecret**
- **RedirectUri**

## 🛠 Công nghệ sử dụng
- **.NET 8.0**
- **Dapper** (SQL access)
- **Azure AD OpenID Connect**
- **JWT** (RS256)
- **Serilog**
- **Swagger**
- **SQL Server**
- **Redis** (chạy trên docker)
- **signalR**

## 🔒 Ghi chú bảo mật
- **Không commit** file `private.key` lên repo public.
- **Refresh token** được hash bằng **SHA256** trước khi lưu DB.
- Cookie `refresh_token` dùng **HttpOnly**, **Secure**, **SameSite=None**, **Domain='.local.com'**.
- Cần **HTTPS** khi chạy production.

## Flow
- Lần đầu User login Azure AD -> ResAuthApi đọc thông tin token lấy Email, Name của user. 
  - Tạo access_token (Exp 1h) nội bộ ký theo chuẩn RAS và cached lại trên MemoryCache.
  - Tạo refresh_token lưu vào DB (Exp 7d)
  - Tạo cookie cho refresh_token theo Domain (Domain = ".local.com")
  - Các FE vào check cookei bằng cách gọi api /refresh nếu ko có thì login

App A login -> nhận access_token + refresh_token -> lưu refresh_token (Secure Storage)
App B mở -> tìm refresh_token -> gọi Auth API /refresh -> nhận access_token mới -> dùng
App A quay lại -> cũng làm như App B -> SSO hoạt động


          +----------------------+                 +-----------------------+
 Web      |  hr.local.com / crm  |                 | Mobile App (RN/Native)|
          +----------+-----------+                 +-----------+-----------+
                     |                                         |
       (chưa token)  |                                         |
           1. /refresh (cookie)                          1'. /refresh (body)
                     |                                         |
                     v                                         v
          +----------+-----------+                 +-----------+-----------+
          |   Auth Service @     |                 |  Auth Service @       |
          | api-auth.local.com   |  (cùng 1 BE)    | api-auth.local.com    |
          +----------+-----------+                 +-----------+-----------+
                     |                                         |
            Đọc cookie refresh_token                 Đọc body.refresh_token
                     |                                         |
             OK -> cấp access_token                  OK -> cấp access_token
                     |                                         |
                     v                                         v
             Web dùng access_token                    Mobile dùng access_token
             (localStorage / memory)                  (SecureStorage/Keychain)


- khi bắm logout thì sẽ logout hết các web hoặc ứng dụng (theo web/mobile), chưa force all

## Hướng dẫn cách chạy dev
1. Cài docker desktop để chạy Redis. Cài xong chạy lệnh bên dưới
   docker pull redis:latest
   docker run -d --name redis -p 6379:6379 redis:latest redis-server --requirepass Resredis@123

2. Tạo mkcert để validate FE
Bước 1 – Cài đặt mkcert

Vào GitHub tải bản cài đặt:
🔗 https://github.com/FiloSottile/mkcert/releases

Tải file .exe phù hợp với Windows (thường là mkcert-vX.X.X-windows-amd64.exe).

Đổi tên thành mkcert.exe, copy vào một thư mục trong PATH (vd: C:\Windows\System32) hoặc để ở project rồi chạy trực tiếp.

Bước 2 – Cài CA (Certificate Authority) local

Mở PowerShell (Run as administrator) và chạy:

mkcert -install


Lệnh này sẽ:

Tạo một CA gốc (root CA) trên máy bạn.

Import vào Windows Trusted Root Certificate Store.

Import vào store của các trình duyệt (Chrome, Edge, v.v.).

Bước 3 – Tạo wildcard certificate

Trong terminal tại thư mục project FE hoặc thư mục lưu cert, chạy:

mkcert "*.local.com"


Kết quả sẽ ra 2 file:

_wildcard.local.com.pem   (certificate)
_wildcard.local.com-key.pem (private key)

## Cách tạo api-auth.local.com.p12
mkcert api-auth.local.com
openssl pkcs12 -export \
  -out api-auth.local.com.p12 \
  -inkey api-auth.local.com-key.pem \
  -in api-auth.local.com.pem \
  -password pass:123456  

add host
127.0.0.1 hr.local.com
127.0.0.1 crm.local.com
127.0.0.1 api-auth.local.com
127.0.0.1 api-hr.local.com
127.0.0.1 api-crm.local.com

Test chạy lên copy link bên dưới dán vào trình duyệt
https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/authorize
?client_id={ClientId}
&response_type=code
&redirect_uri=https://api-auth.local.com/signin-oidc
&response_mode=query
&scope=openid%20profile%20email
&state=12345