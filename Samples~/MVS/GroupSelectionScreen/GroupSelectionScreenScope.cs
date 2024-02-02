using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Extreal.Integration.Messaging.Redis.MVS.GroupSelectionScreen
{
    public class GroupSelectionScreenScope : LifetimeScope
    {
        [SerializeField] private GroupSelectionScreenView groupSelectionScreenView;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(groupSelectionScreenView);

            builder.RegisterEntryPoint<GroupSelectionScreenPresenter>();
        }
    }
}
