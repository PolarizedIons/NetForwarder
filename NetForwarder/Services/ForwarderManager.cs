using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace NetForwarder.Services
{
    public class ForwarderManager
    {
        private Dictionary<string, ServiceForwarder> _services = new Dictionary<string, ServiceForwarder>();

        public ForwarderManager(IConfiguration config, HttpClient httpClient)
        {
            var whitelistEnabled = config.GetSection("Whitelist").GetValue<bool>("Enabled");

            foreach (var serviceConfig in config.GetSection("Services").GetChildren())
            {
                var name = serviceConfig.Key.ToLowerInvariant();
                var target = serviceConfig.Value;
                var whitelistSection = config.GetSection($"Whitelist:Services:{name}");

                _services.Add(name, new ServiceForwarder(httpClient)
                {
                    Name = name,
                    Target = target,
                    WhitelistEnabled = whitelistEnabled,
                    WhitelistUrl = whitelistSection?["IpListUrl"],
                    WhitelistJsonPath = whitelistSection?["IpListJsonPath"],
                });
                
                Log.Information("Created Service '{name}', with url {target}", name, target);
            }
        }

        public IEnumerable<ServiceForwarder> GetServices()
        {
            return _services.Values;
        }

        public ServiceForwarder GetService(string service)
        {
            service = service.ToLowerInvariant();
            return _services.ContainsKey(service) ? _services[service] : null;
        }
    }
}
