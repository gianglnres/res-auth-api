using ResAuthApi.Application.Interfaces;

namespace ResAuthApi.Api.Services
{
    public class RefreshCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RefreshCleanupService> _logger;

        public RefreshCleanupService(IServiceScopeFactory scopeFactory, ILogger<RefreshCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await AlignToTopOfHour(stoppingToken); // chạy đúng phút 0
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
                    await repo.DeleteExpiredAsync(DateTime.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RefreshCleanupService error");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private static async Task AlignToTopOfHour(CancellationToken ct)
        {
            var now = DateTime.Now;
            var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Local).AddHours(1);
            var delay = next - now;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            await Task.Delay(delay, ct);
        }
    }
}
