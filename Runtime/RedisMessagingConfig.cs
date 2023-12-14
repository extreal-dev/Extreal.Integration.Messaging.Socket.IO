using SocketIOClient;

namespace Extreal.Integration.Messaging.Redis
{
    public class RedisMessagingConfig
    {
        public string Url { get; }
        public SocketIOOptions SocketIOOptions { get; }

        public RedisMessagingConfig(string url, SocketIOOptions socketIOOptions)
        {
            Url = url;
            SocketIOOptions = socketIOOptions;
        }
    }
}
