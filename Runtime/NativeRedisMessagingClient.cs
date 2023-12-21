#if !UNITY_WEBGL || UNITY_EDITOR
using Cysharp.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using SocketIOClient;
using Extreal.Integration.Messaging.Common;
using System.Threading;

namespace Extreal.Integration.Messaging.Redis
{
    public class NativeRedisMessagingClient : RedisMessagingClient
    {
        private readonly RedisMessagingConfig redisMessagingConfig;

        private SocketIO ioClient;
        private CancellationTokenSource cancellation;

        public NativeRedisMessagingClient(RedisMessagingConfig messagingConfig) : base()
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

            ioClient.OnDisconnected += DisconnectEventHandler;
            ioClient.On("delete group", DeleteGroupEventHandler);
            ioClient.On("user joined", UserJoinedEventHandler);
            ioClient.On("user leaving", UserLeavingEventHandler);
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

            ioClient.OnDisconnected -= DisconnectEventHandler;

            await ioClient.EmitAsync("leave");

            await ioClient.DisconnectAsync();
            ioClient.Dispose();
            ioClient = null;
            SetJoiningGroupStatus(false);
        }

        protected override void DoReleaseManagedResources()
            => StopSocketAsync().Forget();

        protected override async UniTask<GroupListResponse> DoListGroupsAsync()
        {
            var groupList = default(GroupListResponse);
            await (await GetSocketAsync()).EmitAsync(
                "list groups",
                response => groupList = response.GetValue<GroupListResponse>()
            );
            await UniTask.WaitUntil(() => groupList != null, cancellationToken: cancellation.Token);
            return groupList;
        }

        protected override async UniTask<CreateGroupResponse> DoCreateGroupAsync(GroupConfig groupConfig)
        {
            var createGroupResponse = default(CreateGroupResponse);
            await (await GetSocketAsync()).EmitAsync(
                "create group",
                response => createGroupResponse = response.GetValue<CreateGroupResponse>(),
                groupConfig.GroupName, groupConfig.MaxCapacity
            );
            await UniTask.WaitUntil(() => createGroupResponse != null, cancellationToken: cancellation.Token);
            return createGroupResponse;
        }

        public override async UniTask DeleteGroupAsync(string groupName)
            => await (await GetSocketAsync()).EmitAsync("delete group", groupName);

        protected override async UniTask<string> DoJoinAsync(MessagingJoiningConfig connectionConfig, string localUserId)
        {
            var message = default(string);
            await (await GetSocketAsync()).EmitAsync(
                "join",
                response => message = response.GetValue<string>(),
                localUserId, connectionConfig.GroupName
            );
            await UniTask.WaitUntil(() => message != null, cancellationToken: cancellation.Token);
            return message;
        }

        protected override UniTask DoLeaveAsync()
            => StopSocketAsync();

        [SuppressMessage("Usage", "CC0021")]
        protected override async UniTask DoSendMessageAsync(Message message)
            => await ioClient.EmitAsync("message", message);

        private void DisconnectEventHandler(object sender, string reason)
            => FireOnUnexpectedLeft(reason);

        private void DeleteGroupEventHandler(SocketIOResponse response)
        {
            FireOnLeaving("delete group");
            StopSocketAsync().Forget();
        }

        private void UserJoinedEventHandler(SocketIOResponse response)
        {
            var connectedUserId = response.GetValue<string>();
            FireOnUserJoined(connectedUserId);
        }

        private void UserLeavingEventHandler(SocketIOResponse response)
        {
            var disconnectingUserId = response.GetValue<string>();
            FireOnUserLeaving(disconnectingUserId);
        }

        private void MessageReceivedEventHandler(SocketIOResponse response)
        {
            var message = response.GetValue<Message>();
            FireOnMessageReceived(message.From, message.MessageContent);
        }
    }
}
#endif
