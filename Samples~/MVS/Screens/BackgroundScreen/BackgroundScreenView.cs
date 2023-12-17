using UnityEngine;

namespace Extreal.Integration.Messaging.Redis.MVS.Screens.BackgroundScreen
{
    public class BackgroundScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject screen;

        public void Show() => screen.SetActive(true);

        public void Hide() => screen.SetActive(false);
    }
}
