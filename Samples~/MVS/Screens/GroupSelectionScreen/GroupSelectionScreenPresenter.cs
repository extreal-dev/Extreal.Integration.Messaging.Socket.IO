using System.Linq;
using Cysharp.Threading.Tasks;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Redis;
using Extreal.Integration.Messaging.Common;
using Extreal.Integration.Messaging.Redis.MVS.App;
using Extreal.Integration.Messaging.Redis.MVS.App.Config;
using Extreal.Integration.Messaging.Redis.MVS.App.Stages;
using UniRx;

namespace Extreal.Integration.Messaging.Redis.MVS.Screens.GroupSelectionScreen
{
    public class GroupSelectionScreenPresenter : StagePresenterBase
    {
        private readonly RedisMessagingClient redisMessagingClient;
        private readonly GroupSelectionScreenView groupSelectionScreenView;

        public GroupSelectionScreenPresenter
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            RedisMessagingClient redisMessagingClient,
            GroupSelectionScreenView groupSelectionScreenView
        ) : base(stageNavigator, appState)
        {
            this.redisMessagingClient = redisMessagingClient;
            this.groupSelectionScreenView = groupSelectionScreenView;
        }

        protected override void Initialize
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            CompositeDisposable sceneDisposables
        )
        {
            groupSelectionScreenView.OnModeChanged
                .Subscribe(appState.SetCommunicationMode)
                .AddTo(sceneDisposables);

            groupSelectionScreenView.OnRoleChanged
                .Subscribe(appState.SetRole)
                .AddTo(sceneDisposables);

            groupSelectionScreenView.OnGroupNameChanged
                .Subscribe(appState.SetGroupName)
                .AddTo(sceneDisposables);

            groupSelectionScreenView.OnGroupChanged
                .Subscribe((groupName) =>
                {
                    appState.SetGroupName(groupName);
                })
                .AddTo(sceneDisposables);

            groupSelectionScreenView.OnUpdateButtonClicked
              .Subscribe(async _ =>
                {
                    var groups = await redisMessagingClient.ListGroupsAsync();
                    var groupNames = groups.Select(group => group.Name).ToArray();
                    groupSelectionScreenView.UpdateGroupNames(groupNames);
                    if (groups.Count > 0)
                    {
                        appState.SetGroupName(groups.First().Name);
                        appState.SetGroupId(groups.First().Id);
                    }
                })
                .AddTo(sceneDisposables);

            groupSelectionScreenView.OnGoButtonClicked
                .Subscribe(_ => stageNavigator.ReplaceAsync(StageName.VirtualStage).Forget())
                .AddTo(sceneDisposables);
        }

        protected override void OnStageEntered
        (
            StageName stageName,
            AppState appState,
            CompositeDisposable stageDisposables
        )
        {
            groupSelectionScreenView.Initialize();
            var role = appState.IsHost ? UserRole.Host : UserRole.Client;
            var communicationMode = CommunicationMode.Massively;
            groupSelectionScreenView.SetInitialValues(role, communicationMode);
        }
    }
}
