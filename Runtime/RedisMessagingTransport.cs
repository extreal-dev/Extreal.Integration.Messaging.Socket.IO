using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UniRx;
using System.Text.Json.Serialization;
using Extreal.Integration.Messaging.Common;
using System.Linq;

namespace Extreal.Integration.Messaging.Redis
{
    public abstract class RedisMessagingTransport : DisposableBase, IMessagingTransport
    {
        public bool IsConnected { get; private set; }
        protected void SetConnectStatus(bool isConnected)
            => IsConnected = isConnected;

        public IObservable<string> OnConnected => onConnected;
        private readonly Subject<string> onConnected;
        protected void FireOnConnected(string userId)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnConnected)}: userId={userId}");
            }
            onConnected.OnNext(userId);
        }

        public IObservable<string> OnDisconnecting => onDisconnecting;
        private readonly Subject<string> onDisconnecting;
        protected void FireOnDisconnecting(string reason)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnDisconnecting)}: reason={reason}");
            }
            onDisconnecting.OnNext(reason);
        }

        public IObservable<string> OnUnexpectedDisconnected => onUnexpectedDisconnected;
        private readonly Subject<string> onUnexpectedDisconnected;
        protected void FireOnUnexpectedDisconnected(string reason)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUnexpectedDisconnected)}: reason={reason}");
            }
            IsConnected = false;
            onUnexpectedDisconnected.OnNext(reason);
        }

        public IObservable<Unit> OnConnectionApprovalRejected => onConnectionApprovalRejected;
        private readonly Subject<Unit> onConnectionApprovalRejected;
        protected void FireOnConnectionApprovalRejected()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnConnectionApprovalRejected)}");
            }
            onConnectionApprovalRejected.OnNext(Unit.Default);
        }

        public IObservable<string> OnUserConnected => onUserConnected;
        private readonly Subject<string> onUserConnected;
        protected void FireOnUserConnected(string userId)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserConnected)}: userId={userId}");
            }
            onUserConnected.OnNext(userId);
        }

        public IObservable<string> OnUserDisconnecting => onUserDisconnecting;
        private readonly Subject<string> onUserDisconnecting;
        protected void FireOnUserDisconnecting(string userId)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserDisconnecting)}: userId={userId}");
            }
            onUserDisconnecting.OnNext(userId);
        }

        public IObservable<(string userId, string message)> OnMessageReceived => onMessageReceived;
        private readonly Subject<(string, string)> onMessageReceived;
        protected void FireOnMessageReceived(string userId, string message)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnMessageReceived)}: userId={userId}, message={message}");
            }
            onMessageReceived.OnNext((userId, message));
        }

        protected RedisMessagingConfig MessagingConfig { get; }
        protected string LocalUserId { get; private set; }

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(RedisMessagingTransport));

        [SuppressMessage("Usage", "CC0022")]
        protected RedisMessagingTransport(RedisMessagingConfig messagingConfig)
        {
            onConnected = new Subject<string>().AddTo(disposables);
            onDisconnecting = new Subject<string>().AddTo(disposables);
            onUnexpectedDisconnected = new Subject<string>().AddTo(disposables);
            onUserConnected = new Subject<string>().AddTo(disposables);
            onUserDisconnecting = new Subject<string>().AddTo(disposables);
            onConnectionApprovalRejected = new Subject<Unit>().AddTo(disposables);
            onMessageReceived = new Subject<(string, string)>().AddTo(disposables);

            MessagingConfig = messagingConfig;
        }

        protected override void ReleaseManagedResources()
        {
            disposables.Dispose();
            DoReleaseManagedResources();
        }

        protected abstract void DoReleaseManagedResources();

        public async UniTask<List<Group>> ListGroupsAsync()
        {
            var groupResponses = await DoListGroupsAsync();
            return groupResponses?.Groups.Select(groupResponse => new Group(groupResponse.Id, groupResponse.Name)).ToList()
                ?? new List<Group>();
        }

        protected abstract UniTask<GroupList> DoListGroupsAsync();

        public async UniTask ConnectAsync(MessagingConnectionConfig connectionConfig)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Connect: GroupName={connectionConfig.GroupName}, MaxCapacity={connectionConfig.MaxCapacity}");
            }
            LocalUserId = Guid.NewGuid().ToString();

            var message = await DoConnectAsync(connectionConfig);

            if (message == "rejected")
            {
                FireOnConnectionApprovalRejected();
                return;
            }

            IsConnected = true;
            FireOnConnected(LocalUserId);
        }

        protected abstract UniTask<string> DoConnectAsync(MessagingConnectionConfig connectionConfig);

        public async UniTask DisconnectAsync()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug(nameof(DisconnectAsync));
            }
            FireOnDisconnecting("disconnect request");
            await DoDisconnectAsync();
        }

        protected abstract UniTask DoDisconnectAsync();

        public UniTask DeleteGroupAsync()
            => SendMessageAsync("delete group");

        public async UniTask SendMessageAsync(string jsonMessage, string to = default)
        {
            var message = new Message
            {
                From = LocalUserId,
                To = to,
                MessageContent = jsonMessage
            };
            await DoSendMessageAsync(message);
        }

        [SuppressMessage("Usage", "CC0021")]
        protected abstract UniTask DoSendMessageAsync(Message message);

        [SuppressMessage("Usage", "CC0047")]
        public class GroupList
        {
            [JsonPropertyName("groups")]
            public List<GroupResponse> Groups { get; set; }
        }

        [SuppressMessage("Usage", "CC0047")]
        public class Message
        {
            [JsonPropertyName("from")]
            public string From { get; set; }

            [JsonPropertyName("to")]
            public string To { get; set; }

            [JsonPropertyName("messageContent")]
            public string MessageContent { get; set; }
        }
    }
}
