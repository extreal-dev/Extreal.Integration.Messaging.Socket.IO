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

type WebGLCreateGroupResponse = {
  status: number;
  createGroupResponse: CreateGroupResponse;
}

type CreateGroupResponse = {
  status: number;
  message: string;
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
  onUserJoined: (userId: string) => void;
  onUserLeaving: (userId: string) => void;
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
    this.socket.on("delete group", this.receiveDeleteGroup);
    this.socket.on("user joined", this.receiveUserJoined);
    this.socket.on("user leaving", this.receiveUserLeaving);
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

  public createGroup = (groupName: string, maxCapacity: number, handle: (response: WebGLCreateGroupResponse) => void) => {
    const returnError = () => {
      const ret: WebGLCreateGroupResponse = { status: 504, createGroupResponse: { status: 504, message: "connect error" } };
      handle(ret);
    }
    this.getSocket(returnError).emit("create group", groupName, maxCapacity, (response: CreateGroupResponse) => {
      if (this.isDebug) {
        console.log(response);
      }
      const ret: WebGLCreateGroupResponse = { status: 200, createGroupResponse: response };
      handle(ret);
    });
  };

  public deleteGroup = (groupName: string, handle: (response: number) => void) => {
    const returnError = () => handle(504);
    this.getSocket(returnError).emit("delete group", groupName, (response: number) => {
      handle(response);
    });
  };

  public join = (userId: string, groupName: string, handle: (response: WebGLJoinResponse) => void) => {
    const returnError = () => {
      const ret: WebGLJoinResponse = { status: 504, message: "connect error" };
      handle(ret);
    }
    this.getSocket(returnError).emit("join", userId, groupName, (response: string) => {
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

  private receiveUserJoined = (userId: string) => {
    if (this.isDebug) {
      console.log(`Receive user joined: ${userId}`);
    }
    this.callbacks.onUserJoined(userId);
  };

  private receiveUserLeaving = (userId: string) => {
    if (this.isDebug) {
      console.log(`Receive user leaving: ${userId}`);
    }
    this.callbacks.onUserLeaving(userId);
  };

  private receiveMessageAsync = async (message: Message) => {
    if (this.isDebug) {
      console.log(`Receive message: ${message}`);
    }
    this.callbacks.onMessageReceived(message);
  };
}

export { RedisMessagingClient };
