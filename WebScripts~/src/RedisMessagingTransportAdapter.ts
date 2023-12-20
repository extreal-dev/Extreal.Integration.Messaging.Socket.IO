import { RedisMessagingClient } from "./RedisMessagingClient";
import { addAction, callback } from "@extreal-dev/extreal.integration.web.common";

class RedisMessagingAdapter {
    private redisMessagingClient: RedisMessagingClient | undefined;

    public adapt = () => {
        addAction(this.withPrefix("WebGLRedisMessagingClient"), (jsonRedisMessagingConfig) => {
            const redisMessagingConfig = JSON.parse(jsonRedisMessagingConfig);
            if (redisMessagingConfig.isDebug) {
                console.log(redisMessagingConfig);
            }
            this.redisMessagingClient = new RedisMessagingClient(redisMessagingConfig, {
                setJoiningGroupStatus: (isConnected) =>
                    callback(this.withPrefix("HandleJoiningGroupStatus"), isConnected),
                onLeaving: (reason) => callback(this.withPrefix("HandleOnLeaving"), reason),
                onUnexpectedLeft: (reason) => callback(this.withPrefix("HandleOnUnexpectedLeft"), reason),
                onUserJoined: (userId) => callback(this.withPrefix("HandleOnUserJoined"), userId),
                onUserLeaving: (userId) => callback(this.withPrefix("HandleOnUserLeaving"), userId),
                onMessageReceived: (userId, message) =>
                    callback(this.withPrefix("HandleOnMessageReceived"), userId, message),
            });
        });

        addAction(this.withPrefix("DoReleaseManagedResources"), () =>
            this.getRedisMessagingClient().releaseManagedResources(),
        );

        addAction(this.withPrefix("DoListGroupsAsync"), () =>
            this.getRedisMessagingClient().listGroups((response) =>
                callback(this.withPrefix("ReceiveGroupList"), JSON.stringify(response)),
            ),
        );

        addAction(this.withPrefix("DoCreateGroupAsync"), (groupName, maxCapacity) =>
            this.getRedisMessagingClient().createGroup(groupName, Number.parseInt(maxCapacity), (response) =>
                callback(this.withPrefix("ReceiveCreateGroupMessage"), JSON.stringify(response)),
            ),
        );

        addAction(this.withPrefix("DeleteGroupAsync"), (groupName) =>
            this.getRedisMessagingClient().deleteGroup(groupName),
        );

        addAction(this.withPrefix("DoJoinAsync"), (userId, groupName) =>
            this.getRedisMessagingClient().join(userId, groupName, (response) =>
                callback(this.withPrefix("ReceiveJoinMessage"), response),
            ),
        );

        addAction(this.withPrefix("DoLeaveAsync"), () => this.getRedisMessagingClient().leave());

        addAction(this.withPrefix("DoSendMessageAsync"), (message) =>
            this.getRedisMessagingClient().sendMessage(JSON.parse(message)),
        );
    };

    private withPrefix = (name: string) => `WebGLRedisMessagingClient#${name}`;

    public getRedisMessagingClient = () => {
        if (!this.redisMessagingClient) {
            throw new Error("Call the WebGLRedisMessagingClient constructor first in Unity.");
        }
        return this.redisMessagingClient;
    };
}

export { RedisMessagingAdapter };
