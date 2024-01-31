import { io, Socket, SocketOptions, ManagerOptions } from "socket.io-client";

type RedisMessagingConfig = {
  url: string;
  socketIOOptions: SocketOptions & ManagerOptions;
  isDebug: boolean;
};

type WebGLGroupListResponse = {
  status: number;
  groupListResponse: GroupListResponse;
}

type GroupListResponse = {
  groups: Array<{ id: string; name: string }>;
};

type WebGLJoinResponse = {
  status: number;
  message: string;
}

type Message = {
  from: string;
  to: string;
  messageContent: string;
};

type RedisMessagingClientCallbacks = {
  setJoiningGroupStatus: (isJoinedGroup: string) => void;
  onLeaving: (reason: string) => void;
  onUnexpectedLeft: (reason: string) => void;
  onClientJoined: (clientId: string) => void;
  onClientLeaving: (clientId: string) => void;
  onMessageReceived: (message: Message) => void;
  stopSocket: () => void;
};

class RedisMessagingClient {
  private readonly isDebug: boolean;

  private readonly redisMessagingConfig: RedisMessagingConfig;
  private socket: Socket | null;

  private readonly callbacks: RedisMessagingClientCallbacks;

  constructor(redisMessagingConfig: RedisMessagingConfig, callbacks: RedisMessagingClientCallbacks) {
    this.socket = null;
    this.redisMessagingConfig = redisMessagingConfig;
    this.isDebug = redisMessagingConfig.isDebug;
    this.callbacks = callbacks;
  }

  private getSocket = (connectErrorHandle: () => void) => {
    if (this.socket !== null) {
      if (this.socket.connected) {
        return this.socket;
      }
      this.stopSocket();
    }

    const socket = io(this.redisMessagingConfig.url, this.redisMessagingConfig.socketIOOptions);
    this.socket = socket;

    this.socket.on("disconnect", this.receiveDisconnect);
    this.socket.on("client joined", this.receiveClientJoined);
    this.socket.on("client leaving", this.receiveClientLeaving);
    this.socket.on("message", this.receiveMessageAsync);

    this.socket.on("connect_error", () => {
      console.log("connect error");
      connectErrorHandle();
      if (this.socket) {
        this.socket.disconnect();
        this.socket = null;
      }
    });

    this.socket.connect();

    return this.socket;
  };

  private stopSocket = () => {
    if (this.socket === null) {
      return;
    }

    this.callbacks.stopSocket();

    this.socket.off("disconnect", this.receiveDisconnect);

    this.socket.emit("leave");

    this.socket.disconnect();
    this.socket = null;
    this.callbacks.setJoiningGroupStatus("false");
  };

  public releaseManagedResources = () => {
    this.stopSocket();
  };

  public listGroups = (handle: (response: WebGLGroupListResponse) => void) => {
    const returnError = () => {
      const ret: WebGLGroupListResponse = { status: 504, groupListResponse: { groups: [] } };
      handle(ret);
    }
    this.getSocket(returnError).emit("list groups", (response: GroupListResponse) => {
      if (this.isDebug) {
        console.log(response);
      }
      const ret: WebGLGroupListResponse = { status: 200, groupListResponse: response };
      handle(ret);
    });
  };

  public join = (clientId: string, groupName: string, maxCapacity: number, handle: (response: WebGLJoinResponse) => void) => {
    const returnError = () => {
      const ret: WebGLJoinResponse = { status: 504, message: "connect error" };
      handle(ret);
    }
    this.getSocket(returnError).emit("join", clientId, groupName, maxCapacity, (response: string) => {
      if (this.isDebug) {
        console.log(response);
      }
      const ret: WebGLJoinResponse = { status: 200, message: response };
      handle(ret);
    });
  };

  public leave = () => {
    this.stopSocket();
  };

  public sendMessage = (message: Message) => {
    if (this.socket) {
      this.socket.emit("message", message);
    }
  };

  private receiveDisconnect = (reason: string) => {
    if (this.isDebug) {
      console.log(`Receive disconnect: reason=${reason}`);
    }
    this.callbacks.onUnexpectedLeft(reason);
  };

  public receiveDeleteGroup = () => {
    this.callbacks.onLeaving("delete group");
    this.stopSocket();
  };

  private receiveClientJoined = (clientId: string) => {
    if (this.isDebug) {
      console.log(`Receive client joined: ${clientId}`);
    }
    this.callbacks.onClientJoined(clientId);
  };

  private receiveClientLeaving = (clientId: string) => {
    if (this.isDebug) {
      console.log(`Receive client leaving: ${clientId}`);
    }
    this.callbacks.onClientLeaving(clientId);
  };

  private receiveMessageAsync = async (message: Message) => {
    if (this.isDebug) {
      console.log(`Receive message: ${message}`);
    }
    this.callbacks.onMessageReceived(message);
  };

  public getClientId = (() => this.socket?.id ?? "");
}

export { RedisMessagingClient };
