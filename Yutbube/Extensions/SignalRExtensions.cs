using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;

namespace Yutbube.Extensions
{
    public static class SignalRExtensions
    {
        public static async void Publish(this IAsyncCollector<SignalRMessage> signalR, string target, params object[] args)
        {
            await signalR.AddAsync(new SignalRMessage
            {
                Target = target,
                Arguments = args
            });
        }
    }
}