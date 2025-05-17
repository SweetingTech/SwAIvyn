using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SwAIvyn.Hubs
{
    public class VoiceHub : Hub
    {
        public async Task SendVoiceData(string user, byte[] audioData)
        {
            await Clients.All.SendAsync("ReceiveVoiceData", user, audioData);
        }

        public async Task JoinVoiceRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            await Clients.Group(roomId).SendAsync("UserJoinedVoice", Context.ConnectionId);
        }

        public async Task LeaveVoiceRoom(string roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            await Clients.Group(roomId).SendAsync("UserLeftVoice", Context.ConnectionId);
        }
    }
}
