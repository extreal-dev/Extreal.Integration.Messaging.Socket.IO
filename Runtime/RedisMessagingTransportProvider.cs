namespace Extreal.Integration.Messaging.Redis
{
    public class RedisMessagingTransportProvider
    {
        public static RedisMessagingTransport Provide(RedisMessagingConfig messagingConfig)
#if !UNITY_WEBGL || UNITY_EDITOR
            => new NativeRedisMessagingTransport(messagingConfig);
#else
            => new WebGLRedisMessagingTransport(new WebGLRedisMessagingConfig(messagingConfig));
#endif
    }
}
