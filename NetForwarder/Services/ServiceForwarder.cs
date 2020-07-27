using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NetForwarder.Exceptions;
using Newtonsoft.Json.Linq;
using Serilog;

namespace NetForwarder.Services
{
    public class ServiceForwarder
    {
        public string Name { get; set; }
        public string Target { get; set; }
        public bool WhitelistEnabled { get; set; }
        public string? WhitelistUrl { get; set; }
        public string? WhitelistJsonPath { get; set; }
        public IEnumerable<IPNetwork> WhitelistedIps { get; private set; } = new IPNetwork[0];

        private HttpClient _httpClient;

        public ServiceForwarder(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<HttpResponseMessage> ForwardRequest(HttpRequest request, HttpMethod method)
        {
            var remoteIpAddr = request.HttpContext.Connection.RemoteIpAddress;
            if (!IPAddress.IsLoopback(remoteIpAddr) && !WhitelistedIps.Any(network => network.Contains(remoteIpAddr)))
            {
                throw new ForwardRequestException("IP Address is not whitelisted");
            }

            var targetUri = Target + request.HttpContext.Request.QueryString.Value;

            string body;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, false))
            {
                body = await reader.ReadToEndAsync();
            }

            var contentType = request.Headers.FirstOrDefault(x => x.Key == "Content-Type");
            var forwardingRequest = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(targetUri),
                Content = new StringContent(body, Encoding.UTF8, contentType.Value),
            };

            foreach (var requestHeader in request.Headers)
            {
                if (requestHeader.Key != "Content-Type" && requestHeader.Key != "Content-Length")
                {
                    forwardingRequest.Headers.Add(requestHeader.Key, requestHeader.Value.First());
                }
            }

            try
            {
                Log.Debug("Forwarding from '{remoteIpAddr}' to '{targetUri}' ({method}) for service '{Name}'", remoteIpAddr, targetUri, method, Name);
                var req = await _httpClient.SendAsync(forwardingRequest);

                if (!req.IsSuccessStatusCode)
                {
                    throw new ForwardRequestException($"Forwarding failed with status code: {req.StatusCode}");
                }

                return req;
            }
            catch (HttpRequestException e)
            {
                throw new ForwardRequestException($"Forwarding failed: {e.Message}");
            }
        }

        public async Task PopulateWhitelistedIps()
        {
            if (!WhitelistEnabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(WhitelistUrl))
            {
                Log.Warning($"Service '{Name}' has no whitelisting setup!");
                return;
            }
            
            if (string.IsNullOrEmpty(WhitelistJsonPath))
            {
                WhitelistJsonPath = "[*]";
            }

            Log.Debug("Fetching IPs for service '{Name}'", Name);

            var req = await _httpClient.GetAsync(WhitelistUrl);
            if (!req.IsSuccessStatusCode)
            {
                Log.Warning("Unable to get IP Addresses for service '{Name}'; Server returned a {StatusCode}", Name, req.StatusCode);
                return;
            }

            var body = await req.Content.ReadAsStringAsync();
            JObject responseJson = JObject.Parse(body);

            WhitelistedIps = responseJson.SelectTokens(WhitelistJsonPath)
                .Select(x => IPNetwork.Parse(x.Value<string>()));
            Log.Debug("Loaded {Count} CIDR ranges for service '{Name}'", WhitelistedIps.Count(), Name);
        }
    }
}
