using VContainer;
using VContainer.Unity;
using SocketIOClient;
using Extreal.Integration.Messaging.Redis.MVS.App;

namespace Extreal.Integration.Messaging.Redis.MVS.ClientControl
{
    public class ClientControlScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var redisMessagingConfig = new RedisMessagingConfig("http://localhost:3030", new SocketIOOptions { EIO = EngineIO.V4 });
            var redisMessagingClient1 = RedisMessagingClientProvider.Provide(redisMessagingConfig);
            var redisMessagingClient2 = RedisMessagingClientProvider.Provide(redisMessagingConfig);
            var clientCollection = new ClientCollection(redisMessagingClient1, redisMessagingClient2);

            builder.RegisterComponent(clientCollection);
            builder.RegisterEntryPoint<ClientControlPresenter>();
        }
    }
}
