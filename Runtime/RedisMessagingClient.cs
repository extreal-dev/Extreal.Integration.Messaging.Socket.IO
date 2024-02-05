using Cysharp.Threading.Tasks;
using Extreal.Core.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Extreal.Integration.Messaging;

namespace Extreal.Integration.Messaging.Redis
{
    /// <summary>
    /// Class that implements MessagingClient using Redis.
    /// </summary>
    public abstract class RedisMessagingClient : MessagingClient
    {
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(RedisMessagingClient));

        protected sealed override async UniTask DoJoinAsync(MessagingJoiningConfig joiningConfig)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Join: GroupName={joiningConfig.GroupName}");
            }
            var localUserId = Guid.NewGuid().ToString();

            var message = await DoJoinAsync(joiningConfig, localUserId);

            if (message == "rejected")
            {
                FireOnJoiningApprovalRejected();
                return;
            }

            SetJoiningGroupStatus(true);
            FireOnJoined(localUserId);
        }

        protected abstract UniTask<string> DoJoinAsync(MessagingJoiningConfig connectionConfig, string localUserId);

        protected sealed override async UniTask DoSendMessageAsync(string message, string to)
        {
            var messageObj = new Message
            {
                To = to,
                MessageContent = message
            };
            await DoSendMessageAsync(messageObj);
        }

        protected abstract UniTask DoSendMessageAsync(Message message);

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
