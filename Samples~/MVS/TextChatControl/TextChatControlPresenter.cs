using System.Linq;
using UniRx;
using VContainer.Unity;
using Extreal.Integration.Messaging;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Redis.MVS.App;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Extreal.Integration.Messaging.Redis.MVS.ClientControl;

namespace Extreal.Integration.Messaging.Redis.MVS.TextChatControl
{
    public class TextChatControlPresenter : StagePresenterBase, IInitializable
    {
        private readonly RedisMessagingClient redisMessagingClient1;
        private readonly RedisMessagingClient redisMessagingClient2;
        private readonly TextChatControlView textChatControlView;
        private readonly ClientCollection clientCollection;
        public TextChatControlPresenter
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            ClientCollection clientCollection,
            TextChatControlView textChatControlView,
            AppState appState
        ) : base(stageNavigator, appState)
        {
            redisMessagingClient1 = clientCollection.Clients.First();
            redisMessagingClient2 = clientCollection.Clients.Last();
            this.textChatControlView = textChatControlView;
        }

        protected override void Initialize(StageNavigator<StageName, SceneName> stageNavigator, AppState appState, CompositeDisposable sceneDisposables)
        {
            textChatControlView.OnSendButtonClicked
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Subscribe(message =>
                {
                    textChatControlView.SetFromWhichClient(message.Split('-').ToList().Last());
                    UnityEngine.Debug.Log(message.Split('-').ToList().Last());
                    if (textChatControlView.FromClient1)
                    {
                        redisMessagingClient1.SendMessageAsync(message).Forget();
                    }
                    if (textChatControlView.FromClient2)
                    {
                        redisMessagingClient2.SendMessageAsync(message).Forget();
                    }

                    textChatControlView.ShowSentMessage(message);
                })
                .AddTo(sceneDisposables);

            redisMessagingClient1.OnMessageReceived
                .Subscribe(textChatControlView.ShowReceivedMessage)
                .AddTo(sceneDisposables);

            redisMessagingClient2.OnMessageReceived
                .Subscribe(textChatControlView.ShowReceivedMessage)
                .AddTo(sceneDisposables);

            textChatControlView.Initialize();
        }

        protected override void OnStageEntered(StageName stageName, AppState appState, CompositeDisposable stageDisposables) => UniTask.Void(async () =>
        {
            try
            {
                var messagingConfig = new MessagingJoiningConfig(appState.GroupName, 4);
                await redisMessagingClient1.JoinAsync(messagingConfig);
                await redisMessagingClient2.JoinAsync(messagingConfig);
            }
            catch (Exception e)
            {
                appState.Notify($"Fail to Join a group: {e.Message}");
            }
        });

        protected override void OnStageExiting(StageName stageName, AppState appState) => UniTask.Void(async () =>
        {
            await redisMessagingClient1.LeaveAsync();
            await redisMessagingClient2.LeaveAsync();
        });
    }
}
