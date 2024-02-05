import { RedisMessagingClient } from "./RedisMessagingClient";
import { addAction, callback } from "@extreal-dev/extreal.integration.web.common";

class RedisMessagingAdapter {
  private redisMessagingClients = new Map<string, RedisMessagingClient>();

  public adapt = () => {
    addAction(this.withPrefix("WebGLRedisMessagingClient"), (jsonRedisMessagingConfig, instanceId) => {
      const redisMessagingConfig = JSON.parse(jsonRedisMessagingConfig);
      if (redisMessagingConfig.isDebug) {
        console.log(redisMessagingConfig);
      }
      this.redisMessagingClients.set(
        instanceId,
        new RedisMessagingClient(redisMessagingConfig, {
          setJoiningGroupStatus: (isConnected) =>
            callback(this.withPrefix("HandleJoiningGroupStatus"), isConnected, instanceId),
          onLeaving: (reason) => callback(this.withPrefix("HandleOnLeaving"), reason, instanceId),
          onUnexpectedLeft: (reason) =>
            callback(this.withPrefix("HandleOnUnexpectedLeft"), reason, instanceId),
          onUserJoined: (userId) => callback(this.withPrefix("HandleOnUserJoined"), userId, instanceId),
          onUserLeaving: (userId) => callback(this.withPrefix("HandleOnUserLeaving"), userId, instanceId),
          onMessageReceived: (message) =>
            callback(this.withPrefix("HandleOnMessageReceived"), JSON.stringify(message), instanceId),
          stopSocket: () => callback(this.withPrefix("StopSocket"), instanceId),
        }),
      );
    });

    addAction(this.withPrefix("DoReleaseManagedResources"), (instanceId) => {
      this.getRedisMessagingClient(instanceId).releaseManagedResources();
      this.redisMessagingClients.delete(instanceId);
    });

    addAction(this.withPrefix("DoListGroupsAsync"), (instanceId) =>
      this.getRedisMessagingClient(instanceId).listGroups((response) =>
        callback(this.withPrefix("ReceiveGroupListResponse"), JSON.stringify(response), instanceId),
      ),
    );

    addAction(this.withPrefix("DoCreateGroupAsync"), (groupConfigStr, instanceId) => {
      const groupConfig = JSON.parse(groupConfigStr);
      this.getRedisMessagingClient(instanceId).createGroup(
        groupConfig.groupName,
        groupConfig.maxCapacity,
        (response) =>
          callback(this.withPrefix("ReceiveCreateGroupResponse"), JSON.stringify(response), instanceId),
      );
    });

    addAction(this.withPrefix("DeleteGroupAsync"), (groupName, instanceId) =>
      this.getRedisMessagingClient(instanceId).deleteGroup(groupName, (response) =>
        callback(this.withPrefix("ReceiveDeleteGroupResponse"), JSON.stringify(response), instanceId),
      ));

    addAction(this.withPrefix("DoJoinAsync"), (joiningConfigStr, instanceId) => {
      const joiningConfig = JSON.parse(joiningConfigStr);
      this.getRedisMessagingClient(instanceId).join(joiningConfig.userId, joiningConfig.groupName, (response) =>
        callback(this.withPrefix("ReceiveJoinResponse"), JSON.stringify(response), instanceId),
      );
    });

    addAction(this.withPrefix("DoLeaveAsync"), (instanceId) => this.getRedisMessagingClient(instanceId).leave());

    addAction(this.withPrefix("DoSendMessageAsync"), (message, instanceId) =>
      this.getRedisMessagingClient(instanceId).sendMessage(JSON.parse(message)),
    );
  };

  private withPrefix = (name: string) => `WebGLRedisMessagingClient#${name}`;

  public getRedisMessagingClient = (instanceId: string) => {
    const redisMessagingClient = this.redisMessagingClients.get(instanceId);
    if (!redisMessagingClient) {
      throw new Error("Call the WebGLRedisMessagingClient constructor first in Unity.");
    }
    return redisMessagingClient;
  };
}

export { RedisMessagingAdapter };
