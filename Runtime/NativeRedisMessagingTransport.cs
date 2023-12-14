#if !UNITY_WEBGL || UNITY_EDITOR
using Cysharp.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using SocketIOClient;
using Extreal.Integration.Messaging.Common;
using System.Threading;

namespace Extreal.Integration.Messaging.Redis
{
    public class NativeRedisMessagingTransport : RedisMessagingTransport
    {
        private readonly RedisMessagingConfig redisMessagingConfig;

        private SocketIO ioClient;
        private CancellationTokenSource cancellation;

        public NativeRedisMessagingTransport(RedisMessagingConfig messagingConfig) : base()
            => redisMessagingConfig = messagingConfig;

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

            cancellation = new CancellationTokenSource();

            ioClient = new SocketIO(redisMessagingConfig.Url, redisMessagingConfig.SocketIOOptions);

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

            cancellation.Cancel();
            cancellation.Dispose();

            ioClient.OnDisconnected -= DisconnectedEventHandler;

            await ioClient.EmitAsync("leave");

            ioClient.Dispose();
            ioClient = null;
            SetConnectStatus(false);
        }

        protected override void DoReleaseManagedResources()
            => StopSocketAsync().Forget();

        protected override async UniTask<GroupList> DoListGroupsAsync()
        {
            var groupList = default(GroupList);
            await (await GetSocketAsync()).EmitAsync(
                "list groups",
                response => groupList = response.GetValue<GroupList>()
            );
            await UniTask.WaitUntil(() => groupList != null, cancellationToken: cancellation.Token);
            return groupList;
        }

        protected override async UniTask<string> DoConnectAsync(MessagingConnectionConfig connectionConfig, string localUserId)
        {
            var message = default(string);
            await (await GetSocketAsync()).EmitAsync(
                "join",
                response => message = response.GetValue<string>(),
                localUserId, connectionConfig.GroupName, connectionConfig.MaxCapacity
            );
            await UniTask.WaitUntil(() => message != null, cancellationToken: cancellation.Token);
            return message;
        }

        protected override UniTask DoDisconnectAsync()
            => StopSocketAsync();

        [SuppressMessage("Usage", "CC0021")]
        protected override async UniTask DoSendMessageAsync(Message message)
            => await ioClient.EmitAsync("message", message);

        private void DisconnectedEventHandler(object sender, string reason)
            => FireOnUnexpectedDisconnected(reason);

        private void UserConnectedEventHandler(SocketIOResponse response)
        {
            var connectedUserId = response.GetValue<string>();
            FireOnUserConnected(connectedUserId);
        }

        private void UserDisconnectingEventHandler(SocketIOResponse response)
        {
            var disconnectingUserId = response.GetValue<string>();
            FireOnUserDisconnecting(disconnectingUserId);
        }

        private void MessageReceivedEventHandler(SocketIOResponse response)
        {
            var message = response.GetValue<Message>();

            if (message.MessageContent == "delete group")
            {
                FireOnDisconnecting("delete group");
                StopSocketAsync().Forget();
                return;
            }

            FireOnMessageReceived(message.From, message.MessageContent);
        }
    }
}
#endif
