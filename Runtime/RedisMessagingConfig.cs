using System;
using SocketIOClient;

namespace Extreal.Integration.Messaging.Redis
{
    public class RedisMessagingConfig
    {
        public string Url { get; }
        public SocketIOOptions SocketIOOptions { get; }

        public RedisMessagingConfig(string url, SocketIOOptions socketIOOptions = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            Url = url;
            SocketIOOptions = socketIOOptions ?? new SocketIOOptions();
        }
    }
}
