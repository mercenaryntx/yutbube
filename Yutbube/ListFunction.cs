using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Yutbube.Extensions;
using Yutbube.Repositories;

namespace Yutbube
{
    public static class ListFunction
    {
        [FunctionName("list")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, ILogger log)
        {
            log.LogInformation("List triggered");
            try
            {
                return req.CreateResponse(HttpStatusCode.OK, await BlobStorageRepository.List());
            }
            catch (Exception ex)
            {
                log.LogError(ex, ex.Message.EscapeCurlyBraces());
                return req.CreateResponse(HttpStatusCode.BadRequest, ex);
            }
        }
    }
}