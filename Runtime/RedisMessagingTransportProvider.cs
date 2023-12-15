using System;

namespace Extreal.Integration.Messaging.Redis
{
    public class RedisMessagingTransportProvider
    {
        public static RedisMessagingTransport Provide(RedisMessagingConfig messagingConfig)
        {
            if (messagingConfig == null)
            {
                throw new ArgumentNullException(nameof(messagingConfig));
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            return new NativeRedisMessagingTransport(messagingConfig);
#else
            return new WebGLRedisMessagingTransport(new WebGLRedisMessagingConfig(messagingConfig));
#endif
        }
    }
}
