using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ResAuthApi.Api.Hubs;
using ResAuthApi.Application.Interfaces;
using Serilog;
using System.Security.Claims;

namespace ResAuthApi.Api.Controllers
{
    [Route("mobile")]
    [ApiController]
    public class AuthControllerMobile : ControllerBase
    {
        private readonly IAzureAdService _azureAdService;
        private readonly ITokenService _tokenService;
        private readonly IHubContext<LogoutHub> _hubContext;

        public AuthControllerMobile(IAzureAdService azureAdService, ITokenService tokenService, IHubContext<LogoutHub> hubContext)
        {
            _azureAdService = azureAdService;
            _tokenService = tokenService;
            _hubContext = hubContext;
        }

        // 1. Mobile Login
        [HttpPost("signin-oidc")]
        public async Task<IActionResult> SignInMobile([FromQuery] string code)
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

            var accessToken = _tokenService.GenerateInternalToken(claims, expiresInSeconds: 3600);

            // Lưu refresh token cho mobile
            var refreshRaw = await _tokenService.CreateRefreshTokenAsync(
                email,
                TimeSpan.FromDays(7),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers["User-Agent"].ToString(),
                clientType: "mobile"
            );

            Log.Information("Mobile user {Email} logged in at {Time}", email, DateTime.Now);

            return Ok(new
            {
                access_token = accessToken,
                refresh_token = refreshRaw,
                expires_in = 3600
            });
        }

        // 2. Mobile Refresh
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshMobile([FromBody] RefreshMobileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest(new { error = "Missing refresh_token" });

            var tokenEntity = await _tokenService.FindByRawTokenAsync(request.RefreshToken);
            if (tokenEntity == null || tokenEntity.ExpiresAt < DateTime.Now || tokenEntity.RevokedAt != null || tokenEntity.IsRevoked)
                return BadRequest(new { error = "Invalid or expired refresh token" });

            // Rotate token
            var newRaw = await _tokenService.RotateAsync(tokenEntity, TimeSpan.FromDays(7));

            var claims = new[] { new Claim("email", tokenEntity.Email) };
            var newAccess = _tokenService.GenerateInternalToken(claims, expiresInSeconds: 3600);

            Log.Information("Mobile user {Email} refreshed token at {Time}", tokenEntity.Email, DateTime.Now);

            return Ok(new
            {
                access_token = newAccess,
                refresh_token = newRaw,
                expires_in = 3600
            });
        }

        // 3. Mobile Logout
        [HttpPost("logout")]
        public async Task<IActionResult> LogoutMobile([FromBody] LogoutMobileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest(new { error = "Missing refresh_token" });

            var tokenEntity = await _tokenService.FindByRawTokenAsync(request.RefreshToken);
            if (tokenEntity != null)
            {
                tokenEntity.IsRevoked = true;
                // 1. Revoke token hiện tại
                await _tokenService.RevokeAsync(tokenEntity, "logout");

                // 2. Gửi SignalR thông báo logout toàn bộ mobile
                await _hubContext.Clients
                    .Group($"{tokenEntity.Email}:mobile") // Nhóm kết nối theo email
                    .SendAsync("Logout", new { reason = "User requested logout" });

                Log.Information("Mobile user {Email} logged out at {Time}", tokenEntity.Email, DateTime.Now);
            }

            return Ok(new { message = "Logged out successfully" });
        }
    }

    // DTO request model
    public class RefreshMobileRequest
    {
        public string RefreshToken { get; set; }
    }

    public class LogoutMobileRequest
    {
        public string RefreshToken { get; set; }
    }
}