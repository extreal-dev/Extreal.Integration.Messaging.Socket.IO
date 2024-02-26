using System;
using System.Diagnostics.CodeAnalysis;
using Extreal.Core.Common.System;

namespace Extreal.Integration.Messaging.Socket.IO.MVS.App
{
    public class ClientCollection : DisposableBase
    {
        public SocketIOMessagingClient[] Clients { get; }

        [SuppressMessage("Style", "CC0057")]
        public ClientCollection(params SocketIOMessagingClient[] clients) => Clients = clients;

        protected override void ReleaseManagedResources() => Array.ForEach(Clients, client => client.Dispose());
    }
}
