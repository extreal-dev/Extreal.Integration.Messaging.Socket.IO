import { io, Socket, SocketOptions, ManagerOptions } from "socket.io-client";

type RedisMessagingConfig = {
    url: string;
    socketOptions: SocketOptions & ManagerOptions;
    isDebug: boolean;
};

type MessagingConnectionConfig = {
    userId: string;
    groupName: string;
    macCapacity: number;
};

type GroupList = {
    groups: Array<{ id: string; name: string }>;
};

type Message = {
    from: string;
    to: string;
    messageContent: string;
};

type RedisMessagingTransportCallbacks = {
    setConnectStatus: (isConnected: string) => void;
    onConnected: (userId: string) => void;
    onDisconnecting: (reason: string) => void;
    onUnexpectedDisconnected: (reason: string) => void;
    onConnectionApprovalRejected: () => void;
    onUserConnected: (userId: string) => void;
    onUserDisconnecting: (userId: string) => void;
    onMessageReceived: (userId: string, message: string) => void;
};

class RedisMessagingTransport {
    private readonly isDebug: boolean;

    private readonly redisMessagingConfig: RedisMessagingConfig;
    private socket: Socket | null;

    private readonly callbacks: RedisMessagingTransportCallbacks;

    constructor(redisMessagingConfig: RedisMessagingConfig, callbacks: RedisMessagingTransportCallbacks) {
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

        const socket = io(this.redisMessagingConfig.url, this.redisMessagingConfig.socketOptions);
        this.socket = socket;

        this.socket.on("disconnect", this.receiveDisconnect);
        this.socket.on("user connected", this.receiveUserConnected);
        this.socket.on("user disconnecting", this.receiveUserDisconnecting);
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

        this.socket.close();
        this.socket = null;
        this.callbacks.setConnectStatus("false");
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

    public connectAsync = (connectionConfig: MessagingConnectionConfig, handle: (response: string) => void) => {
        this.getSocket().emit(
            "join",
            connectionConfig.userId,
            connectionConfig.groupName,
            connectionConfig.macCapacity,
            (response: string) => {
                if (this.isDebug) {
                    console.log(response);
                }
                handle(response);
            },
        );
    };

    public disconnectAsync = () => {
        this.stopSocket();
    };

    public sendMessage = (messageJson: string) => {
        if (this.socket) {
            const message = JSON.parse(messageJson);
            this.socket.emit("message", message);
        }
    };

    private receiveDisconnect = (reason: string) => {
        if (this.isDebug) {
            console.log(reason);
        }
        this.callbacks.onUnexpectedDisconnected(reason);
    };

    private receiveUserConnected = (userId: string) => {
        if (this.isDebug) {
            console.log(userId);
        }
        this.callbacks.onUserConnected(userId);
    };

    private receiveUserDisconnecting = (userId: string) => {
        if (this.isDebug) {
            console.log(`Receive user disconnected: ${userId}`);
        }
        this.callbacks.onUserDisconnecting(userId);
    };

    private receiveMessageAsync = async (message: Message) => {
        if (this.isDebug) {
            console.log(`Receive message: ${message}`);
        }

        if (message.messageContent === "delete group") {
            this.callbacks.onDisconnecting("delete group");
            this.stopSocket();
            return;
        }

        this.callbacks.onMessageReceived(message.from, message.messageContent);
    };
}

export { RedisMessagingTransport };
