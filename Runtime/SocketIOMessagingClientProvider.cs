using System;

namespace Extreal.Integration.Messaging.Socket.IO
{
    /// <summary>
    /// Class that provides SocketIOMessagingClient.
    /// </summary>
    public static class SocketIOMessagingClientProvider
    {
        /// <summary>
        /// Provides SocketIOMessagingClient.
        /// </summary>
        /// <remarks>
        /// Creates and returns a SocketIOMessagingClient for Native (C#) or WebGL (JavaScript) depending on the platform.
        /// </remarks>
        /// <param name="messagingConfig">Messaging config for Socket.IO.</param>
        /// <exception cref="ArgumentNullException">When messagingConfig is null.</exception>
        /// <returns>SocketIOMessagingClient.</returns>
        public static SocketIOMessagingClient Provide(SocketIOMessagingConfig messagingConfig)
        {
            if (messagingConfig == null)
            {
                // Not covered by testing but passed by peer review
                throw new ArgumentNullException(nameof(messagingConfig));
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            return new NativeSocketIOMessagingClient(messagingConfig);
#else
            return new WebGLSocketIOMessagingClient(new WebGLSocketIOMessagingConfig(messagingConfig));
#endif
        }
    }
}
