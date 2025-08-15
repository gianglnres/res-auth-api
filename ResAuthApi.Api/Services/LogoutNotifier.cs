using Microsoft.AspNetCore.SignalR;
using ResAuthApi.Api.Hubs;

namespace ResAuthApi.Api.Services
{
    public class LogoutNotifier
    {
        private readonly IHubContext<LogoutHub> _hubContext;

        public LogoutNotifier(IHubContext<LogoutHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyLogout(string userId, int targetType)
        {
            switch (targetType)
            {
                case 1: // chỉ web
                    await _hubContext.Clients.Group($"{userId}:web").SendAsync("Logout");
                    break;

                case 2: // chỉ mobile
                    await _hubContext.Clients.Group($"{userId}:mobile").SendAsync("Logout");
                    break;

                case 3: // cả web và mobile
                    await _hubContext.Clients.Group($"{userId}:all").SendAsync("Logout");
                    break;
            }
        }

    }
}
