import { SocketIOMessagingClient } from "./SocketIOMessagingClient";
import { addAction, addFunction, callback } from "@extreal-dev/extreal.integration.web.common";

class SocketIOMessagingAdapter {
  private socketIOMessagingClients = new Map<string, SocketIOMessagingClient>();

  public adapt = () => {
    addAction(this.withPrefix("WebGLSocketIOMessagingClient"), (jsonSocketIOMessagingConfig, instanceId) => {
      const socketIOMessagingConfig = JSON.parse(jsonSocketIOMessagingConfig);
      if (socketIOMessagingConfig.isDebug) {
        console.log(socketIOMessagingConfig);
      }
      this.socketIOMessagingClients.set(
        instanceId,
        new SocketIOMessagingClient(socketIOMessagingConfig, {
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
      this.getSocketIOMessagingClient(instanceId).releaseManagedResources();
      this.socketIOMessagingClients.delete(instanceId);
    });

    addAction(this.withPrefix("DoListGroupsAsync"), (instanceId) =>
      this.getSocketIOMessagingClient(instanceId).listGroups((response) =>
        callback(this.withPrefix("ReceiveGroupListResponse"), JSON.stringify(response), instanceId),
      ),
    );

    addAction(this.withPrefix("DoJoinAsync"), (joiningConfigStr, instanceId) => {
      const joiningConfig = JSON.parse(joiningConfigStr);
      this.getSocketIOMessagingClient(instanceId).join(joiningConfig.groupName, (response) =>
        callback(this.withPrefix("ReceiveJoinResponse"), JSON.stringify(response), instanceId),
      );
    });

    addAction(this.withPrefix("DoLeaveAsync"), (instanceId) => this.getSocketIOMessagingClient(instanceId).leave());

    addAction(this.withPrefix("DoSendMessageAsync"), (message, instanceId) =>
      this.getSocketIOMessagingClient(instanceId).sendMessage(JSON.parse(message)),
    );

    addFunction(this.withPrefix("GetClientId"), (instanceId) => this.getSocketIOMessagingClient(instanceId).getClientId())
  };

  private withPrefix = (name: string) => `WebGLSocketIOMessagingClient#${name}`;

  public getSocketIOMessagingClient = (instanceId: string) => {
    const socketIOMessagingClient = this.socketIOMessagingClients.get(instanceId);
    if (!socketIOMessagingClient) {
      throw new Error("Call the WebGLSocketIOMessagingClient constructor first in Unity.");
    }
    return socketIOMessagingClient;
  };
}

export { SocketIOMessagingAdapter };
