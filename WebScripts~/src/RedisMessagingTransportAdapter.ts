import { RedisMessagingTransport } from "./RedisMessagingTransport";
import { addAction, callback } from "@extreal-dev/extreal.integration.web.common";

type RedisMessagingTransportProvider = () => RedisMessagingTransport;

class RedisMessagingTransportAdapter {
    private redisMessagingTransport: RedisMessagingTransport | undefined;

    public adapt = () => {
        addAction(this.withPrefix("WebGLRedisMessagingTransport"), (jsonRedisMessagingConfig) => {
            const redisMessagingConfig = JSON.parse(jsonRedisMessagingConfig);
            if (redisMessagingConfig.isDebug) {
                console.log(redisMessagingConfig);
            }
            this.redisMessagingTransport = new RedisMessagingTransport(redisMessagingConfig, {
                setConnectStatus: (isConnected) => callback(this.withPrefix("HandleConnectStatus"), isConnected),
                onDisconnecting: (reason) => callback(this.withPrefix("HandleOnDisconnecting"), reason),
                onUnexpectedDisconnected: (reason) =>
                    callback(this.withPrefix("HandleOnUnexpectedDisconnected"), reason),
                onUserConnected: (userId) => callback(this.withPrefix("HandleOnUserConnected"), userId),
                onUserDisconnecting: (userId) => callback(this.withPrefix("HandleOnUserDisconnecting"), userId),
                onMessageReceived: (userId, message) =>
                    callback(this.withPrefix("HandleOnMessageReceived"), userId, message),
            });
        });

        addAction(this.withPrefix("DoReleaseManagedResources"), () => {
            this.getRedisMessagingTransport().releaseManagedResources();
        });

        addAction(this.withPrefix("DoListGroupsAsync"), () => {
            this.getRedisMessagingTransport().listGroups((response) =>
                callback(this.withPrefix("ReceiveGroupList"), JSON.stringify(response)),
            );
        });

        addAction(this.withPrefix("DoConnectAsync"), (connectionConfig) =>
            this.getRedisMessagingTransport().connectAsync(JSON.parse(connectionConfig), (response) =>
                callback(this.withPrefix("ReceiveConnectMessage"), response),
            ),
        );

        addAction(this.withPrefix("DoDisconnectAsync"), () => this.getRedisMessagingTransport().disconnectAsync());

        addAction(this.withPrefix("DoSendMessageAsync"), (message) =>
            this.getRedisMessagingTransport().sendMessage(message),
        );
    };

    private withPrefix = (name: string) => `WebGLRedisMessagingTransport#${name}`;

    public getRedisMessagingTransport: RedisMessagingTransportProvider = () => {
        if (!this.redisMessagingTransport) {
            throw new Error("Call the WebGLRedisMessagingTransport constructor first in Unity.");
        }
        return this.redisMessagingTransport;
    };
}

export { RedisMessagingTransportAdapter, RedisMessagingTransportProvider };
