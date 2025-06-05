using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SwAIvyn.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task SendNotification(string user, string message, string type)
        {
            await Clients.All.SendAsync("ReceiveNotification", user, message, type);
        }

        public async Task SendUserNotification(string userId, string message, string type)
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", "system", message, type);
        }

        public async Task SubscribeToNotifications(string category)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, category);
        }

        public async Task UnsubscribeFromNotifications(string category)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, category);
        }
    }
}
