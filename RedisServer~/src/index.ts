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
const isDebug = process.env.LOGGING === "on";

// const promClient = require('prom-client');
const register = new promClient.Registry();
// - Default metrics are collected on scrape of metrics endpoint, not on an
//  interval. The `timeout` option to `collectDefaultMetrics(conf)` is no longer
//  supported or needed, and the function no longer returns a `Timeout` object.
promClient.collectDefaultMetrics({ register: register });

const app = express();
app.use(express.json());
// CROS対応
app.use(cors());
app.get("/", (req, res) => {
    res.send("OK");
});
app.get("/metrics", async (req, res) => {
    res.setHeader("Content-Type", register.contentType);
    const metrics = await register.metrics();
    res.send(metrics);
});

type Message = {
    from: string;
    to: string;
};

type ListGroupsResponse = {
    groups: Group[];
};

type Group = {
    id: string;
    name: string;
};

const httpServer = createServer(app);

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
    if (isDebug) {
        console.log(`Start on port ${apiPort}`);
    }
});
const subClient = pubClient.duplicate();
subClient.on("error", (err) => {
    if (isDebug) {
        console.log("Redis subClient Error", err);
    }
});
io.adapter(createAdapter(pubClient, subClient)); // redis-adapter

const rooms = (): Map<string, Set<string>> => {
    // @ts-ignore See https://socket.io/docs/v4/rooms/#implementation-details
    return io.sockets.adapter.rooms;
};

io.on("connection", async (socket: Socket) => {
    let groupName = "";
    let userId = "";

    socket.on(
        "join",
        async (
            receivedUserId: string,
            receivedGroupName: string,
            receivedMaxCapacity: number,
            callback: (response: string) => void,
        ) => {
            groupName = receivedGroupName;
            userId = receivedUserId;

            if (
                ![...rooms().entries()]
                    .filter((entry) => !entry[1].has(entry[0]))
                    .find((entry) => entry[0] === groupName) &&
                receivedMaxCapacity !== 0
            ) {
                await redisClient.set(`MaxCapacity#${groupName}`, receivedMaxCapacity);
            }

            const maxCapacityStr = await redisClient.get(`MaxCapacity#${groupName}`);
            if (maxCapacityStr) {
                const maxCapacity = Number.parseInt(maxCapacityStr);
                const connectedClientNum = rooms().get(groupName)?.size as number;
                if (connectedClientNum >= maxCapacity) {
                    if (isDebug) {
                        console.log(`Reject user: ${userId}`);
                    }
                    callback("rejected");
                    return;
                }
            }

            callback("approved");
            if (isDebug) {
                console.log(`join: userId=${userId}, groupName=${groupName}`);
            }
            await redisClient.set(userId, socket.id.toString());
            await socket.join(groupName);
            socket.to(groupName).emit("user connected", userId);

            return;
        },
    );

    socket.on("message", async (message: Message) => {
        message.from = userId;
        if (message.to) {
            const socketId = await redisClient.get(message.to);
            if (socketId) {
                socket.to(socketId).emit("message", message);
            }
            return;
        }
        if (groupName) {
            socket.to(groupName).emit("message", message);
        }
    });

    socket.on("list groups", (callback: (response: ListGroupsResponse) => void) => {
        callback({
            groups: [...rooms().entries()]
                .filter((entry) => !entry[1].has(entry[0]))
                .map((entry) => ({ name: entry[0], id: [...entry[1]][0] })),
        });
    });

    const handleDisconnect = () => {
        if (groupName) {
            if (isDebug) {
                console.log(`user disconnecting[${socket.id}]`);
            }
            socket.to(groupName).emit("user disconnecting", userId);
            socket.leave(groupName);
            groupName = "";
        }
    };

    socket.on("leave", handleDisconnect);

    socket.on("disconnect", () => {
        if (isDebug) {
            console.log("disconnect");
        }
        handleDisconnect();
    });

    const redisClient = createClient({ url: redisUrl }).on("error", (err) => {
        console.error("Redis Client Error:%o", err);
        process.exit(1);
    });

    await redisClient.connect();
    if (isDebug) {
        console.log(`worker: connected id: ${socket.id}`);
    }
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
        if (isDebug) {
            console.log(`Socket.io Listen: ${appPort}`);
            console.log("=================================Restarted======================================");
        }
    });
