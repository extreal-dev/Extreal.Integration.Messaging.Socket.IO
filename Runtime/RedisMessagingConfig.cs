using System;
using SocketIOClient;

namespace Extreal.Integration.Messaging.Redis
{
    /// <summary>
    /// Class that holds Messaging configuration for Redis.
    /// </summary>
    public class RedisMessagingConfig
    {
        /// <summary>
        /// URL of the messaging server.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Socket.IO options.
        /// </summary>
        public SocketIOOptions SocketIOOptions { get; }

        /// <summary>
        /// Creates a new redis messaging configuration.
        /// </summary>
        /// <param name="url">URL of the messaging server.</param>
        /// <param name="socketIOOptions">Socket.IO options.</param>
        /// <exception cref="ArgumentNullException">When url is null.</exception>
        public RedisMessagingConfig(string url, SocketIOOptions socketIOOptions = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                // Not covered by testing but passed by peer review
                throw new ArgumentNullException(nameof(url));
            }

            Url = url;
            SocketIOOptions = socketIOOptions ?? new SocketIOOptions();
        }
    }
}
