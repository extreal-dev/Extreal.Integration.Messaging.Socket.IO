using Extreal.Integration.Messaging.Common;
using SocketIOClient;

namespace Extreal.Integration.Messaging.Redis
{
    public class RedisMessagingConfig : MessagingConfig
    {
        public SocketIOOptions SocketIOOptions { get; }

        public RedisMessagingConfig(string url, SocketIOOptions options) : base(url)
            => SocketIOOptions = options;
    }
}
