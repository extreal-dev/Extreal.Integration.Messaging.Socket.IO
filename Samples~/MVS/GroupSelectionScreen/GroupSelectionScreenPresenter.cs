using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Redis.MVS.App;
using UniRx;

namespace Extreal.Integration.Messaging.Redis.MVS.GroupSelectionScreen
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
            groupSelectionScreenView.OnRoleChanged
                .Subscribe(appState.SetRole)
                .AddTo(sceneDisposables);

            groupSelectionScreenView.OnGroupNameChanged
                .Subscribe(appState.SetGroupName)
                .AddTo(sceneDisposables);

            groupSelectionScreenView.OnGroupChanged
                .Subscribe(appState.SetGroupName)
                .AddTo(sceneDisposables);

            groupSelectionScreenView.OnUpdateButtonClicked
              .Subscribe(async _ =>
                {
                    try
                    {
                        var groups = await redisMessagingClient.ListGroupsAsync();
                        var groupNames = groups.Select(group => group.Name).ToArray();
                        groupSelectionScreenView.UpdateGroupNames(groupNames);
                        if (groups.Count > 0)
                        {
                            appState.SetGroupName(groups.First().Name);
                        }
                    }
                    catch (Exception e)
                    {
                        appState.Notify($"Failed to list groups: {e.Message}");
                    }
                })
                .AddTo(sceneDisposables);

            groupSelectionScreenView.OnGoButtonClicked
                .Subscribe(_ => stageNavigator.ReplaceAsync(StageName.VirtualStage).Forget())
                .AddTo(sceneDisposables);

            groupSelectionScreenView.OnBackButtonClicked
                .Subscribe(_ => stageNavigator.ReplaceAsync(StageName.TitleStage).Forget())
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
            groupSelectionScreenView.SetInitialValues(role);
        }
    }
}
