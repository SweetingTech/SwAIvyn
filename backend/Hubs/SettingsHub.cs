using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SwAIvyn.Hubs
{
    /// <summary>
    /// Hub used to notify clients when settings change so they can refresh cached values.
    /// </summary>
    public interface ISettingsClient
    {
        /// <summary>
        /// Triggered when any user setting is updated.
        /// </summary>
        /// <returns>A task that completes when the notification has been sent.</returns>
        Task SettingsChanged();
    }

    /// <summary>
    /// SignalR hub for broadcasting settings updates.
    /// </summary>
    [Authorize]
    public class SettingsHub : Hub<ISettingsClient>
    {
    }
}
