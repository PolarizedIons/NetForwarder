using System.Threading.Tasks;
using Quartz;
using Serilog;

namespace NetForwarder.Services
{
    public class IPWhitelistUpdater : IJob
    {
        private readonly ForwarderManager _forwarderManager;

        public IPWhitelistUpdater(ForwarderManager forwarderManager)
        {
            _forwarderManager = forwarderManager;
        }
        
        public async Task Execute(IJobExecutionContext context)
        {
            Log.Debug("Updating Services' IP Whitelists...");
            foreach (var service in _forwarderManager.GetServices())
            {
                await service.PopulateWhitelistedIps();
            }
        }
    }
}
