using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Yutbube.Extensions;

namespace Yutbube
{
    public static class TerminateFunction
    {
        [FunctionName("terminate")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, ILogger log)
        {
            log.LogInformation("Terminate triggered");
            try
            {
                var invocationId = req.GetQueryNameValuePairs().FirstOrDefault(kvp => kvp.Key == "id").Value;
                Cancellation.Tokens.AddOrUpdate(Guid.Parse(invocationId), true, (key, oldValue) => true);
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                log.LogError(ex, ex.Message.EscapeCurlyBraces());
                return req.CreateResponse(HttpStatusCode.BadRequest, ex);
            }
        }
    }
}