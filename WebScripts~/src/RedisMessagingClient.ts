import { io, Socket, SocketOptions, ManagerOptions } from "socket.io-client";

type RedisMessagingConfig = {
    url: string;
    socketIOOptions: SocketOptions & ManagerOptions;
    isDebug: boolean;
};

type CreateGroupResponse = {
    status: number;
    message: string;
};

type GroupList = {
    groups: Array<{ id: string; name: string }>;
};

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
    onMessageReceived: (userId: string, message: string) => void;
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

    private getSocket = () => {
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

        this.socket.connect();

        return this.socket;
    };

    private stopSocket = () => {
        if (this.socket === null) {
            return;
        }

        this.socket.off("disconnect", this.receiveDisconnect);

        this.socket.emit("leave");

        this.socket.disconnect();
        this.socket = null;
        this.callbacks.setJoiningGroupStatus("false");
    };

    public releaseManagedResources = () => {
        this.stopSocket();
    };

    public listGroups = (handle: (response: GroupList) => void) => {
        this.getSocket().emit("list groups", (response: GroupList) => {
            if (this.isDebug) {
                console.log(response);
            }
            handle(response);
        });
    };

    public createGroup = (groupName: string, maxCapacity: number, handle: (response: CreateGroupResponse) => void) => {
        this.getSocket().emit("create group", groupName, maxCapacity, (response: CreateGroupResponse) => {
            if (this.isDebug) {
                console.log(response);
            }
            handle(response);
        });
    };

    public deleteGroup = (groupName: string) => {
        this.getSocket().emit("delete group", groupName);
    };

    public join = (userId: string, groupName: string, handle: (response: string) => void) => {
        this.getSocket().emit("join", userId, groupName, (response: string) => {
            if (this.isDebug) {
                console.log(response);
            }
            handle(response);
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
        this.callbacks.onMessageReceived(message.from, message.messageContent);
    };
}

export { RedisMessagingClient };
