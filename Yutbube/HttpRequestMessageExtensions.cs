using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Yutbube
{
    public static class HttpRequestMessageExtensions
    {
        public static Dictionary<string, string> GetQueryNameValuePairs(this HttpRequestMessage req)
        {
            return req.RequestUri.Query.TrimStart('?').Split('&').Select(p => p.Split('=')).ToDictionary(pp => pp[0], pp => pp[1]);
        }
    }
}