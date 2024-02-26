using VContainer;
using VContainer.Unity;
using SocketIOClient;
using Extreal.Integration.Messaging.Socket.IO.MVS.App;

namespace Extreal.Integration.Messaging.Socket.IO.MVS.ClientControl
{
    public class ClientControlScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var socketIOMessagingConfig = new SocketIOMessagingConfig("http://localhost:3030", new SocketIOOptions { EIO = EngineIO.V4 });
            var socketIOMessagingClient1 = SocketIOMessagingClientProvider.Provide(socketIOMessagingConfig);
            var socketIOMessagingClient2 = SocketIOMessagingClientProvider.Provide(socketIOMessagingConfig);
            var clientCollection = new ClientCollection(socketIOMessagingClient1, socketIOMessagingClient2);

            builder.RegisterComponent(clientCollection);
            builder.RegisterEntryPoint<ClientControlPresenter>();
        }
    }
}
