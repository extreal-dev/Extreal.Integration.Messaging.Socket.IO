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
    /// <summary>
    /// Class that implement IMessagingTransport using Redis.
    /// </summary>
    public abstract class RedisMessagingTransport : DisposableBase, IMessagingTransport
    {
        /// <inheritdoc/>
        public bool IsConnected { get; private set; }
        protected void SetConnectStatus(bool isConnected)
            => IsConnected = isConnected;

        /// <inheritdoc/>
        public IObservable<string> OnConnected => onConnected;
        private readonly Subject<string> onConnected;
        protected void FireOnConnected(string userId) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnConnected)}: userId={userId}");
            }
            onConnected.OnNext(userId);
        });

        /// <inheritdoc/>
        public IObservable<string> OnDisconnecting => onDisconnecting;
        private readonly Subject<string> onDisconnecting;
        protected void FireOnDisconnecting(string reason) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnDisconnecting)}: reason={reason}");
            }
            onDisconnecting.OnNext(reason);
        });

        /// <inheritdoc/>
        public IObservable<string> OnUnexpectedDisconnected => onUnexpectedDisconnected;
        private readonly Subject<string> onUnexpectedDisconnected;
        protected void FireOnUnexpectedDisconnected(string reason) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUnexpectedDisconnected)}: reason={reason}");
            }
            IsConnected = false;
            onUnexpectedDisconnected.OnNext(reason);
        });

        /// <inheritdoc/>
        public IObservable<Unit> OnConnectionApprovalRejected => onConnectionApprovalRejected;
        private readonly Subject<Unit> onConnectionApprovalRejected;
        protected void FireOnConnectionApprovalRejected() => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnConnectionApprovalRejected)}");
            }
            onConnectionApprovalRejected.OnNext(Unit.Default);
        });

        /// <inheritdoc/>
        public IObservable<string> OnUserConnected => onUserConnected;
        private readonly Subject<string> onUserConnected;
        protected void FireOnUserConnected(string userId) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserConnected)}: userId={userId}");
            }
            onUserConnected.OnNext(userId);
        });

        /// <inheritdoc/>
        public IObservable<string> OnUserDisconnecting => onUserDisconnecting;
        private readonly Subject<string> onUserDisconnecting;
        protected void FireOnUserDisconnecting(string userId) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserDisconnecting)}: userId={userId}");
            }
            onUserDisconnecting.OnNext(userId);
        });

        /// <inheritdoc/>
        public IObservable<(string userId, string message)> OnMessageReceived => onMessageReceived;
        private readonly Subject<(string, string)> onMessageReceived;
        protected void FireOnMessageReceived(string userId, string message) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnMessageReceived)}: userId={userId}, message={message}");
            }
            onMessageReceived.OnNext((userId, message));
        });

        private string localUserId;

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(RedisMessagingTransport));

        [SuppressMessage("Usage", "CC0022")]
        protected RedisMessagingTransport()
        {
            onConnected = new Subject<string>().AddTo(disposables);
            onDisconnecting = new Subject<string>().AddTo(disposables);
            onUnexpectedDisconnected = new Subject<string>().AddTo(disposables);
            onUserConnected = new Subject<string>().AddTo(disposables);
            onUserDisconnecting = new Subject<string>().AddTo(disposables);
            onConnectionApprovalRejected = new Subject<Unit>().AddTo(disposables);
            onMessageReceived = new Subject<(string, string)>().AddTo(disposables);
        }

        protected override void ReleaseManagedResources()
        {
            disposables.Dispose();
            DoReleaseManagedResources();
        }

        protected abstract void DoReleaseManagedResources();

        /// <inheritdoc/>
        public async UniTask<List<Group>> ListGroupsAsync()
        {
            var groupList = await DoListGroupsAsync();
            return groupList.Groups.Select(groupResponse => new Group(groupResponse.Id, groupResponse.Name)).ToList();
        }

        protected abstract UniTask<GroupList> DoListGroupsAsync();

        /// <inheritdoc/>
        public async UniTask ConnectAsync(MessagingConnectionConfig connectionConfig)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Connect: GroupName={connectionConfig.GroupName}, MaxCapacity={connectionConfig.MaxCapacity}");
            }
            localUserId = Guid.NewGuid().ToString();

            var message = await DoConnectAsync(connectionConfig, localUserId);

            if (message == "rejected")
            {
                FireOnConnectionApprovalRejected();
                return;
            }

            IsConnected = true;
            FireOnConnected(localUserId);
        }

        protected abstract UniTask<string> DoConnectAsync(MessagingConnectionConfig connectionConfig, string localUserId);

        /// <inheritdoc/>
        public UniTask DisconnectAsync()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug(nameof(DisconnectAsync));
            }
            FireOnDisconnecting("disconnect request");
            return DoDisconnectAsync();
        }

        protected abstract UniTask DoDisconnectAsync();

        /// <inheritdoc/>
        public UniTask DeleteGroupAsync()
            => SendMessageAsync("delete group");

        /// <inheritdoc/>
        public async UniTask SendMessageAsync(string message, string to = default)
        {
            if (!IsConnected)
            {
                if (Logger.IsWarn())
                {
                    Logger.LogWarn("Called Send method before connecting to a group");
                }
                return;
            }

            var messageObj = new Message
            {
                To = to,
                MessageContent = message
            };
            await DoSendMessageAsync(messageObj);
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
