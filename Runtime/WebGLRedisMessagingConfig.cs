#if UNITY_WEBGL
using Extreal.Core.Logging;

namespace Extreal.Integration.Messaging.Redis
{
    public class WebGLRedisMessagingConfig : RedisMessagingConfig
    {
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(WebGLRedisMessagingConfig));

        public WebGLRedisMessagingConfig(RedisMessagingConfig messagingConfig)
            : base(messagingConfig.Url, messagingConfig.SocketIOOptions)
        {
        }

        public bool IsDebug => Logger.IsDebug();
    }
}
#endif
