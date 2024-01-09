using System.Linq;
using UniRx;
using VContainer.Unity;
using Extreal.Integration.Messaging;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Redis.MVS.App;
using Cysharp.Threading.Tasks;
using System;

namespace Extreal.Integration.Messaging.Redis.MVS.TextChatControl
{
    public class TextChatControlPresenter : StagePresenterBase, IInitializable
    {
        private readonly RedisMessagingClient redisMessagingClient;
        private readonly TextChatControlView textChatControlView;
        public TextChatControlPresenter
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            RedisMessagingClient redisMessagingClient,
            TextChatControlView textChatControlView,
            AppState appState
        ) : base(stageNavigator, appState)
        {
            this.redisMessagingClient = redisMessagingClient;
            this.textChatControlView = textChatControlView;
        }

        protected override void Initialize(StageNavigator<StageName, SceneName> stageNavigator, AppState appState, CompositeDisposable sceneDisposables)
        {
            textChatControlView.OnSendButtonClicked
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Subscribe(message =>
                {
                    redisMessagingClient.SendMessageAsync(message).Forget();
                    textChatControlView.ShowSentMessage(message);
                })
                .AddTo(sceneDisposables);

            redisMessagingClient.OnMessageReceived
                .Subscribe(textChatControlView.ShowMessage)
                .AddTo(sceneDisposables);

            textChatControlView.Initialize();
        }

        protected override void OnStageEntered(StageName stageName, AppState appState, CompositeDisposable stageDisposables) => UniTask.Void(async () =>
        {
            if (appState.IsHost)
            {
                try
                {
                    var groupConfig = new GroupConfig(appState.GroupName, 2);
                    await redisMessagingClient.CreateGroupAsync(groupConfig);
                }
                catch (Exception e)
                {
                    appState.Notify($"Fail to create group: {e.Message}");
                    return;
                }
            }

            try
            {
                var messagingConfig = new MessagingJoiningConfig(appState.GroupName);
                await redisMessagingClient.JoinAsync(messagingConfig);
            }
            catch (Exception e)
            {
                appState.Notify($"Fail to Join a group: {e.Message}");
            }
        });

        protected override void OnStageExiting(StageName stageName, AppState appState) => UniTask.Void(async () =>
        {
            if (appState.IsHost)
            {
                try
                {
                    await redisMessagingClient.DeleteGroupAsync(appState.GroupName);
                }
                catch (Exception e)
                {
                    appState.Notify($"Fail to delete a group: {e.Message}");
                }
            }
            else
            {
                await redisMessagingClient.LeaveAsync();
            }
        });
    }
}
