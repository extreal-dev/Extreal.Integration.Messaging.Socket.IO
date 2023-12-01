import express from "express";
import { createServer } from "http";
import cors from "cors";
import "dotenv/config";

import { Server, Socket } from "socket.io";
import { createAdapter } from "@socket.io/redis-adapter";
import { createClient } from "redis";
import * as promClient from "prom-client";

const appPort = Number(process.env.APP_PORT) || 3030;
const apiPort = Number(process.env.API_PORT) || 3031;
const redisHost = process.env.REDIS_HOST || "localhost";
const redisPort = Number(process.env.REDIS_PORT) || 7379;

const register = new promClient.Registry();
promClient.collectDefaultMetrics({ register: register });

const app = express();
app.use(express.json());
// CROS対応
app.use(cors());
app.get("/", (_req, res) => {
    res.send("OK");
});
app.get("/metrics", async (_req, res) => {
    res.setHeader("Content-Type", register.contentType);
    const metrics = await register.metrics();
    res.send(metrics);
});

const httpServer = createServer(app);

class Vector3 {
    X: number;
    Y: number;
    Z: number;
    constructor(X: number, Y: number, Z: number) {
        this.X = X;
        this.Y = Y;
        this.Z = Z;
    }
}

class Vector2 {
    X: number;
    Y: number;
    constructor(X: number, Y: number) {
        this.X = X;
        this.Y = Y;
    }
}

class Quaternion {
    X: number;
    Y: number;
    Z: number;
    W: number;
    constructor(X: number, Y: number, Z: number, W: number) {
        this.X = X;
        this.Y = Y;
        this.Z = Z;
        this.W = W;
    }
}

class NetworkObjectInfo {
    ObjectId: string;
    InstanceId: number;
    Position: Vector3;
    Rotation: Quaternion;
    Values: MultiplayPlayerInputValues;
    PlayerInput_Move: Vector2;
    PlayerInput_Look: Vector2;
    PlayerInput_Jump: boolean;
    PlayerInput_Sprint: boolean;
    constructor() {
        this.ObjectId = "";
        this.InstanceId = -1;
        this.Position = new Vector3(0.0, 0.0, 0.0);
        this.Rotation = new Quaternion(0.0, 0.0, 0.0, 0.0);
        this.Values = new MultiplayPlayerInputValues();
        this.PlayerInput_Move = new Vector2(0.0, 0.0);
        this.PlayerInput_Look = new Vector2(0.0, 0.0);
        this.PlayerInput_Jump = false;
        this.PlayerInput_Sprint = false;
    }
}

class MultiplayPlayerInputValues {
    Move: Vector2;
    constructor() {
        this.Move = new Vector2(0.0, 0.0);
    }
}

const MultiplayMessageCommand = {
    None: 0,
    Join: 1,
    Create: 2,
    Update: 3,
    Delete: 4,
};

type MultiplayMessageCommand = typeof MultiplayMessageCommand[keyof typeof MultiplayMessageCommand];

class Message {
    userIdentity: string;
    topic: string;
    multiplayMessageCommand: MultiplayMessageCommand;
    networkObjectInfo: NetworkObjectInfo;
    networkObjectInfos: NetworkObjectInfo[];
    message: string;
    constructor(
        userIdentity: string,
        topic: string,
        networkObjectInfo: NetworkObjectInfo,
        networkObjectInfos: NetworkObjectInfo[],
        message: string,
    ) {
        this.userIdentity = userIdentity;
        this.topic = topic;
        this.multiplayMessageCommand = 0;
        this.networkObjectInfo = networkObjectInfo;
        this.networkObjectInfos = networkObjectInfos;
        this.message = message;
    }
    ToJson() {
        return JSON.stringify(this);
    }
}

type JoinMessage = {
    userIdentity: string;
    roomName: string;
};

const io = new Server(httpServer, {
    allowEIO3: true,
    cors: {
        origin: "*",
    },
});

const redisUrl = `redis://${redisHost}:${redisPort}`;
const pubClient = createClient({ url: redisUrl }).on("error", (err) => {
    console.error("Redis pubClient Error:%o", err);
    process.exit(1);
});
app.listen(apiPort, () => {
    console.log(`Start on port ${apiPort}`);
});
const subClient = pubClient.duplicate();
subClient.on("error", (err) => {
    console.log("Redis subClient Error", err);
});
io.adapter(createAdapter(pubClient, subClient));

io.on("connection", async (socket: Socket) => {
    // memo:この時に、ルームにいるひとにuser connectedの emit("user connected", "use識別子がはいったmessage")
    //　サーバでuse識別子を作成しｍ、各clientに返す
    // messageイベントリスナ

    let userIdentity: string | null = null;
    let roomName: string | null = null;

    socket.on("join", async (msg: string) => {
        const msgObj = JSON.parse(msg) as JoinMessage;
        roomName = msgObj.roomName;
        userIdentity = msgObj.userIdentity;

        io.to(roomName).emit("user connected", userIdentity);
        await socket.join(roomName);
    });

    socket.on("message", async (msg: string) => {
        if (roomName) io.to(roomName).emit("message", msg);
    });

    socket.on("disconnect", () => {
        if (roomName) io.to(roomName).emit("user disconnecting", userIdentity);
    });

    socket.on("delete room", async () => {
        // wip
        if (roomName) io.to(roomName).emit("delete room");
    });

    const redisClient = createClient({ url: redisUrl }).on("error", (err) => {
        console.error("Redis Client Error:%o", err);
        process.exit(1);
    });
    await redisClient.connect();
    console.log(`worker: connected id: ${socket.id}`);
});

Promise.all([pubClient.connect(), subClient.connect()])
    .then(() => {
        io.adapter(createAdapter(pubClient, subClient));
        io.listen(appPort);
    })
    .catch((err) => {
        console.error("Socket.io Listen Error: %o", err);
    })
    .finally(() => {
        console.log(`Socket.io Listen: ${appPort}`);
    });
