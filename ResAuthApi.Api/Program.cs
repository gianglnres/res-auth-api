using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ResAuthApi.Api.Hubs;
using ResAuthApi.Api.Middleware;
using ResAuthApi.Api.Services;
using ResAuthApi.Api.Utils;
using ResAuthApi.Application.Interfaces;
using ResAuthApi.Infrastructure;
using ResAuthApi.Infrastructure.Persistence;
using Serilog;
using StackExchange.Redis;

// Cấu hình log
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning) // Chỉ log Warning+ của Microsoft
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning) // Giảm bớt log từ System
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/resauthapi-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        shared: true
    )
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// cấu hình để chạy ssl ở local
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(7016, listenOptions =>
    {
        listenOptions.UseHttps("../certs/api-auth.local.com.p12", "123456");
    });
});

// Serilog
builder.Host.UseSerilog();

// Config
var cfg = builder.Configuration;

// Đọc connection string từ appsettings.json
string redisCon = cfg.GetConnectionString("Redis")!;
// Khởi tạo Redis ConnectionMultiplexer dùng để cached
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisCon));

// Dapper infra
builder.Services.AddSingleton(new MySqlConnectionFactory(cfg.GetConnectionString("DefaultConnection")!));
builder.Services.AddScoped<IRefreshTokenRepository, DapperRefreshTokenRepository>();

// HttpClient + Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAzureAdService, AzureAdService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// RSA signing key (private)
var privateKeyPath = cfg["Jwt:PrivateKeyPath"]!;
var rsa = KeyLoader.LoadPrivateKeyFromPem(privateKeyPath);
var signingKey = new RsaSecurityKey(rsa) { KeyId = Guid.NewGuid().ToString() };
builder.Services.AddSingleton<RsaSecurityKey>(signingKey);

// AuthN (nếu cần verify nội bộ ở chính ResAuthApi)
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey
        };
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS cho FE localhost:3000
builder.Services.AddCors(o =>
{
    o.AddPolicy("Spa", p => p
        .WithOrigins("https://crm.local.com:3000", "https://hr.local.com:3001")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Background cleanup
builder.Services.AddHostedService<RefreshCleanupService>();

// Add SignalR
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Spa");

// Middleware API key
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map hub
app.MapHub<LogoutHub>("/hubs/logout");

app.Run();
