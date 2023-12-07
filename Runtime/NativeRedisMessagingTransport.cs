#if !UNITY_WEBGL || UNITY_EDITOR
using Cysharp.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using SocketIOClient;
using Extreal.Integration.Messaging.Common;
using System.Text.Json;

namespace Extreal.Integration.Messaging.Redis
{
    public class NativeRedisMessagingTransport : RedisMessagingTransport
    {
        private SocketIO ioClient;

        public NativeRedisMessagingTransport(RedisMessagingConfig messagingConfig) : base(messagingConfig)
        {
        }

        private async UniTask<SocketIO> GetSocketAsync()
        {
            if (ioClient is not null)
            {
                if (ioClient.Connected)
                {
                    return ioClient;
                }
                // Not covered by testing due to defensive implementation
                await StopSocketAsync();
            }

            ioClient = new SocketIO(MessagingConfig.Url, MessagingConfig.SocketIOOptions);

            ioClient.OnDisconnected += DisconnectedEventHandler;
            ioClient.On("user connected", UserConnectedEventHandler);
            ioClient.On("user disconnecting", UserDisconnectingEventHandler);
            ioClient.On("message", MessageReceivedEventHandler);

            try
            {
                await ioClient.ConnectAsync().ConfigureAwait(true);
            }
            catch (ConnectionException)
            {
                throw;
            }

            return ioClient;
        }

        private async UniTask StopSocketAsync()
        {
            if (ioClient is null)
            {
                // Not covered by testing due to defensive implementation
                return;
            }

            ioClient.OnDisconnected -= DisconnectedEventHandler;

            await ioClient.EmitAsync("disconnecting");

            ioClient.Dispose();
            ioClient = null;
            SetConnectStatus(false);
        }

        protected override void DoReleaseManagedResources()
            => StopSocketAsync().Forget();

        protected override async UniTask<RoomList> DoListRoomsAsync()
        {
            var roomList = default(RoomList);
            await (await GetSocketAsync()).EmitAsync(
                "list rooms",
                response => roomList = response.GetValue<RoomList>()
            );
            await UniTask.WaitUntil(() => roomList != null);
            return roomList;
        }

        protected override async UniTask<string> DoConnectAsync(MessagingConnectionConfig connectionConfig)
        {
            var message = default(string);
            await (await GetSocketAsync()).EmitAsync(
                "join",
                response => message = response.GetValue<string>(),
                UserIdentityLocal, connectionConfig.RoomName, connectionConfig.MaxCapacity
            );

            await UniTask.WaitUntil(() => message != null);

            return message;
        }

        protected override UniTask DoDisconnectAsync()
            => StopSocketAsync();

        [SuppressMessage("Usage", "CC0021")]
        protected override async UniTask DoSendMessageAsync(string message)
            => await ioClient.EmitAsync("message", message);

        private void DisconnectedEventHandler(object sender, string e) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            FireOnUnexpectedDisconnected(e);
        });

        private void UserConnectedEventHandler(SocketIOResponse response) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            var connectedUserId = response.GetValue<string>();
            FireOnUserConnected(connectedUserId);
        });

        private void UserDisconnectingEventHandler(SocketIOResponse response) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            var disconnectingUserId = response.GetValue<string>();
            FireOnUserDisconnecting(disconnectingUserId);
        });

        private void MessageReceivedEventHandler(SocketIOResponse response) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            var dataStr = response.GetValue<string>();
            var message = JsonSerializer.Deserialize<Message>(dataStr);

            if (message.MessageContent == "delete room")
            {
                FireOnDisconnecting("delete room");
                StopSocketAsync().Forget();
                return;
            }

            FireOnMessageReceived(message.From, message.MessageContent);
        });
    }
}
#endif
