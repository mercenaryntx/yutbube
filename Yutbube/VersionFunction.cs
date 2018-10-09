using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Yutbube
{
    public static class VersionFunction
    {
        [FunctionName("version")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, ILogger log)
        {
            log.LogInformation("Version triggered");
            try
            {
                var assembly = Assembly.GetAssembly(typeof(VersionFunction));
                var assemblyName = assembly.GetName();
                return req.CreateResponse(HttpStatusCode.OK, assemblyName.Version.ToString());
            }
            catch (Exception ex)
            {
                log.Log(LogLevel.Error, ex.Message, ex);
                return req.CreateResponse(HttpStatusCode.BadRequest, ex);
            }
        }
    }
}