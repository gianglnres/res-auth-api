using Microsoft.AspNetCore.SignalR;

namespace ResAuthApi.Api.Hubs
{
    public class LogoutHub : Hub
    {
        public override async Task OnConnectedAsync()
        {            
            var email = Context.GetHttpContext()?.Request.Query["email"].ToString().ToLower()
                    ?? Context.ConnectionId;
            var deviceType = Context.GetHttpContext()?.Request.Query["deviceType"].ToString().ToLower();

            if (string.IsNullOrEmpty(deviceType))
                deviceType = "web"; // mặc định là web

            // Add vào group theo user + loại device
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{email}:{deviceType}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{email}:all");

            await base.OnConnectedAsync();
        }
    }
}