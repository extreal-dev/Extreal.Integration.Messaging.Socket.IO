using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Redis.MVS.App;
using UniRx;

namespace Extreal.Integration.Messaging.Redis.MVS.ClientControl
{
    public class ClientControlPresenter : StagePresenterBase
    {
        private readonly RedisMessagingClient redisMessagingClient;

        [SuppressMessage("CodeCracker", "CC0057")]
        public ClientControlPresenter
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            RedisMessagingClient redisMessagingClient
        ) : base(stageNavigator, appState)
            => this.redisMessagingClient = redisMessagingClient;

        protected override void Initialize
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            CompositeDisposable sceneDisposables)
        {
            redisMessagingClient.OnJoined
                .Subscribe(userId => appState.NotifyInfo($"Joined: userId={userId}"))
                .AddTo(sceneDisposables);

            redisMessagingClient.OnLeaving
                .Subscribe(reason => appState.NotifyInfo($"Leaving: reason={reason}"))
                .AddTo(sceneDisposables);

            redisMessagingClient.OnUnexpectedLeft
                .Subscribe(reason => appState.Notify($"Unexpectedly left: reason={reason}"))
                .AddTo(sceneDisposables);

            redisMessagingClient.OnJoiningApprovalRejected
                .Subscribe(_ => appState.Notify("Group is full."))
                .AddTo(sceneDisposables);

            redisMessagingClient.OnUserJoined
                .Subscribe(userId => appState.NotifyInfo($"User joined: userId={userId}"))
                .AddTo(sceneDisposables);

            redisMessagingClient.OnUserLeaving
                .Subscribe(userId => appState.NotifyInfo($"User is leaving: userId={userId}"))
                .AddTo(sceneDisposables);
        }
    }
}
