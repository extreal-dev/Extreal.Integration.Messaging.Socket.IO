import { RedisMessagingClient } from "./RedisMessagingClient";
import { addAction, addFunction, callback } from "@extreal-dev/extreal.integration.web.common";

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
          onLeaving: (reason) => callback(this.withPrefix("HandleOnLeaving"), reason, instanceId),
          onUnexpectedLeft: (reason) =>
            callback(this.withPrefix("HandleOnUnexpectedLeft"), reason, instanceId),
          onClientJoined: (clientId) => callback(this.withPrefix("HandleOnClientJoined"), clientId, instanceId),
          onClientLeaving: (clientId) => callback(this.withPrefix("HandleOnClientLeaving"), clientId, instanceId),
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

    addAction(this.withPrefix("DoJoinAsync"), (joiningConfigStr, instanceId) => {
      const joiningConfig = JSON.parse(joiningConfigStr);
      this.getRedisMessagingClient(instanceId).join(joiningConfig.groupName, (response) =>
        callback(this.withPrefix("ReceiveJoinResponse"), JSON.stringify(response), instanceId),
      );
    });

    addAction(this.withPrefix("DoLeaveAsync"), (instanceId) => this.getRedisMessagingClient(instanceId).leave());

    addAction(this.withPrefix("DoSendMessageAsync"), (message, instanceId) =>
      this.getRedisMessagingClient(instanceId).sendMessage(JSON.parse(message)),
    );

    addFunction(this.withPrefix("GetClientId"), (instanceId) => this.getRedisMessagingClient(instanceId).getClientId())
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
