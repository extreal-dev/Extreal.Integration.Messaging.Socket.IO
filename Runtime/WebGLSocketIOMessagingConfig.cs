#if UNITY_WEBGL
using Extreal.Core.Logging;

namespace Extreal.Integration.Messaging.Socket.IO
{
    public class WebGLSocketIOMessagingConfig : SocketIOMessagingConfig
    {
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(WebGLSocketIOMessagingConfig));

        public WebGLSocketIOMessagingConfig(SocketIOMessagingConfig messagingConfig)
            : base(messagingConfig.Url, messagingConfig.SocketIOOptions)
        {
        }

        public bool IsDebug => Logger.IsDebug();
    }
}
#endif
