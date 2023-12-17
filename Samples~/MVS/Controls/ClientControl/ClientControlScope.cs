using VContainer;
using VContainer.Unity;
using SocketIOClient;
using Extreal.Integration.Messaging.Common;

namespace Extreal.Integration.Messaging.Redis.MVS.Controls.ClientControl
{
    public class ClientControlScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var redisMessagingConfig = new RedisMessagingConfig("http://localhost:3030", new SocketIOOptions { EIO = EngineIO.V4 });
            var redisMessagingTransport = RedisMessagingTransportProvider.Provide(redisMessagingConfig);

            var messagingGroupManager = new GroupManager();
            messagingGroupManager.SetTransport(redisMessagingTransport);
            builder.RegisterComponent(messagingGroupManager);

            var messagingClient = new MessagingClient();
            messagingClient.SetTransport(redisMessagingTransport);

            builder.RegisterComponent(messagingClient);
            builder.RegisterEntryPoint<ClientControlPresenter>();
        }
    }
}
