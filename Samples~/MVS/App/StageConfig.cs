using Extreal.Core.StageNavigation;
using UnityEngine;

namespace Extreal.Integration.Messaging.Redis.MVS.App
{
    [CreateAssetMenu(
        menuName = nameof(MVS) + "/" + nameof(StageConfig),
        fileName = nameof(StageConfig))]
    public class StageConfig : StageConfigBase<StageName, SceneName>
    {
    }
}
