using System;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    public interface IAgentService
    {
        Task StartAgentAsync(Guid agentId);
        Task StopAgentAsync(Guid agentId);
    }
}
