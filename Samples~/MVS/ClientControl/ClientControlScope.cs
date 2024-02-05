using VContainer;
using VContainer.Unity;
using SocketIOClient;

namespace Extreal.Integration.Messaging.Redis.MVS.ClientControl
{
    public class ClientControlScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var redisMessagingConfig = new RedisMessagingConfig("http://localhost:3030", new SocketIOOptions { EIO = EngineIO.V4 });
            var redisMessagingClient = RedisMessagingClientProvider.Provide(redisMessagingConfig);

            builder.RegisterComponent(redisMessagingClient);
            builder.RegisterEntryPoint<ClientControlPresenter>();
        }
    }
}
