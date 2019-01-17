using System;
using System.Collections.Concurrent;

namespace Yutbube
{
    public static class Cancellation
    {
        public static ConcurrentDictionary<Guid, bool> Tokens = new ConcurrentDictionary<Guid, bool>();
    }
}