using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NetForwarder.Exceptions;
using NetForwarder.Services;

namespace NetForwarder.Controllers
{
    [ApiController]
    public class ForwardingController : ControllerBase
    {
        private const string RoutePath = "{serviceName:regex(^[[a-zA-Z\\-]]+$)}";

        private readonly ForwarderManager _forwarderManager;

        public ForwardingController(ForwarderManager forwarderManager)
        {
            _forwarderManager = forwarderManager;
        }

        [HttpGet("/")]
        [HttpPost("/")]
        [HttpPut("/")]
        [HttpPatch("/")]
        [HttpDelete("/")]
        public ActionResult Root()
        {
            return BadRequest("No service provided");
        }

        [HttpGet(RoutePath)]
        public async Task<ActionResult> ForwardGet([FromRoute] string serviceName)
        {
            return await Forward(serviceName, HttpMethod.Get);
        }

        [HttpPost(RoutePath)]
        public async Task<ActionResult> ForwardPost([FromRoute] string serviceName)
        {
            return await Forward(serviceName, HttpMethod.Post);
        }

        [HttpPut(RoutePath)]
        public async Task<ActionResult> ForwardPut([FromRoute] string serviceName)
        {
            return await Forward(serviceName, HttpMethod.Put);
        }

        [HttpPatch(RoutePath)]
        public async Task<ActionResult> ForwardPatch([FromRoute] string serviceName)
        {
            return await Forward(serviceName, HttpMethod.Patch);
        }

        [HttpDelete(RoutePath)]
        public async Task<ActionResult> ForwardDelete([FromRoute] string serviceName)
        {
            return await Forward(serviceName, HttpMethod.Delete);
        }
        
        [Route("{*url}", Order = 999)]
        public ActionResult CatchAll()
        {
            return BadRequest("No such service");
        }


        private async Task<ActionResult> Forward(string serviceName, HttpMethod method)
        {
            var service = _forwarderManager.GetService(serviceName);
            if (service == null)
            {
                return BadRequest($"Unknown Service {serviceName}");
            }

            try
            {
                var forwardedRequest = await service.ForwardRequest(Request, method);

                foreach (var header in forwardedRequest.Content.Headers)
                {
                    if (header.Key != "Content-Length")
                    {
                        Response.Headers.Add(header.Key, header.Value.First());
                    }
                }

                return new ObjectResult(await forwardedRequest.Content.ReadAsStreamAsync())
                {
                    StatusCode = (int?) forwardedRequest.StatusCode,
                };
            }
            catch (ForwardRequestException e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
