using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ResAuthApi.Api.Hubs;
using ResAuthApi.Api.Services;
using ResAuthApi.Application.Interfaces;
using Serilog;
using System.Security.Claims;
using System.Text.Json;

namespace ResAuthApi.Api.Controllers
{
    [ApiController]
    [Route("")]
    public class AuthController : ControllerBase
    {
        private readonly IAzureAdService _azureAdService;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _cfg;
        private readonly IHubContext<LogoutHub> _hubContext;

        public AuthController(IAzureAdService azureAdService, ITokenService tokenService, IConfiguration cfg, IHubContext<LogoutHub> hubContext)
        {
            _azureAdService = azureAdService;
            _tokenService = tokenService;
            _cfg = cfg;
            _hubContext = hubContext;
        }

        [HttpGet("signin-oidc")]
        public async Task<IActionResult> SignInOidc([FromQuery] string code)
        {
            if (string.IsNullOrEmpty(code))
                return BadRequest(new { error = "Code is missing" });

            var idToken = await _azureAdService.ExchangeCodeForIdToken(code);
            if (string.IsNullOrEmpty(idToken))
                return BadRequest(new { error = "No id_token" });

            var claims = _azureAdService.ExtractUserClaims(idToken).ToList();
            var email = claims.FirstOrDefault(c => c.Type == "email")?.Value;
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { error = "Email missing" });

            // access token 1h
            var accessToken = _tokenService.GenerateInternalToken(claims, expiresInSeconds: 3600);

            // Log lại thông tin đăng nhận thành công
            Log.Information("User {Email} logged in at {Time}", email, DateTime.Now);

            // refresh token 7 days -> cookie HttpOnly
            var refreshRaw = await _tokenService.CreateRefreshTokenAsync(
                email,
                TimeSpan.FromDays(7),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers["User-Agent"].ToString(),
                clientType: "web"
            );


            Response.Cookies.Append("refresh_token", refreshRaw, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,                // localhost HTTPS
                SameSite = SameSiteMode.None, // FE ở :3000 call BE :7016 => cross-site
                Expires = DateTimeOffset.Now.AddDays(7),
                Domain = _cfg["Domain"]!
            });

            // trả JSON qua postMessage (stringify để an toàn)
            var payload = JsonSerializer.Serialize(new { access_token = accessToken, expires_in = 3600 });
            return Content($@"
                    <html><body>
                    <script>
                      window.opener.postMessage({payload}, '*');
                      window.close();
                    </script>
                    </body></html>", "text/html");
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refresh_token", out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                Log.Warning("Refresh token request missing cookie at {Time}", DateTime.Now);
                return BadRequest(new { error = "Missing refresh_token cookie" });
            }

            var tokenEntity = await _tokenService.FindByRawTokenAsync(raw);
            if (tokenEntity == null || tokenEntity.ExpiresAt < DateTime.Now || tokenEntity.RevokedAt != null || tokenEntity.IsRevoked)
            {
                Log.Warning("Invalid or expired refresh token for email {Email} at {Time}", tokenEntity?.Email, DateTime.Now);
                return BadRequest(new { error = "Invalid or expired refresh token" });
            }

            // rotate refresh token
            var newRaw = await _tokenService.RotateAsync(tokenEntity, TimeSpan.FromDays(7));
            Log.Information("User {Email} refreshed token at {Time}", tokenEntity.Email, DateTime.Now);

            Response.Cookies.Append("refresh_token", newRaw, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.Now.AddDays(7),
                Domain = _cfg["Domain"]!
            });

            // cấp access token mới
            var claims = new[]
            {
                new Claim("email", tokenEntity.Email),
            };
            var newAccess = _tokenService.GenerateInternalToken(claims, expiresInSeconds: 3600);
            return Ok(new { access_token = newAccess, expires_in = 3600 });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (!Request.Cookies.TryGetValue("refresh_token", out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                Log.Warning("Logout request missing cookie at {Time}", DateTime.Now);
                return BadRequest(new { error = "Missing refresh_token cookie" });
            }

            var tokenEntity = await _tokenService.FindByRawTokenAsync(raw);
            if (tokenEntity != null)
            {
                tokenEntity.IsRevoked = true;
                // 1. Revoke refresh token hiện tại
                await _tokenService.RevokeAsync(tokenEntity, "logout");

                // 2. Gửi SignalR thông báo logout toàn bộ web
                await _hubContext.Clients
                    .Group($"{tokenEntity.Email}:web") // Nhóm kết nối theo email:web
                    .SendAsync("Logout", new { reason = "User requested logout" });

                Log.Information("User {Email} logged out at {Time}", tokenEntity.Email, DateTime.Now);
            }

            // Xóa cookie refresh token
            Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Domain = _cfg["Domain"]!
            });

            return Ok(new { message = "Logged out successfully" });
        }

    }
}
