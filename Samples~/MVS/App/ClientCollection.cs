using System;
using System.Diagnostics.CodeAnalysis;
using Extreal.Core.Common.System;

namespace Extreal.Integration.Messaging.Redis.MVS.App
{
    public class ClientCollection : DisposableBase
    {
        public RedisMessagingClient[] Clients { get; }

        [SuppressMessage("Style", "CC0057")]
        public ClientCollection(params RedisMessagingClient[] clients) => Clients = clients;

        protected override void ReleaseManagedResources() => Array.ForEach(Clients, client => client.Dispose());
    }
}
