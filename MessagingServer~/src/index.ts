import express from "express";
import { createServer } from "http";
import cors from "cors";
import "dotenv/config";

import { Server, Socket } from "socket.io";
import { createAdapter } from "@socket.io/redis-adapter";
import { createClient , RedisClientType } from "redis";

const appPort = Number(process.env.APP_PORT) || 3030;
const apiPort = Number(process.env.API_PORT) || 3031;
const redisHost = process.env.REDIS_HOST || "localhost";
const redisPort = Number(process.env.REDIS_PORT) || 7379;
const isLogging = process.env.LOGGING === "on";

const app = express();
app.use(express.json());
app.use(cors());

class RedisClient {
  private client: RedisClientType;

  constructor(url: string) {
    this.client = createClient({ url });
    this.client.on('error', (err) => console.error('Redis Client Error:', err));
  }

  async connect() {
    await this.client.connect();
  }

  async getGroupList(): Promise<Map<string, string>> {
    const groupListStr = await this.client.get("GroupList");
    if (groupListStr) {
      return new Map<string, string>(Object.entries(JSON.parse(groupListStr)));
    }
    return new Map();
  }

  async setGroupList(groupList: Map<string, string>): Promise<void> {
    await this.client.set("GroupList", JSON.stringify(Object.fromEntries(groupList)));
  }

  async getMaxCapacity(groupName: string): Promise<number | null> {
    const maxCapacityStr = await this.client.get(`MaxCapacity#${groupName}`);
    return maxCapacityStr ? parseInt(maxCapacityStr, 10) : null;
  }

  async setMaxCapacity(groupName: string, maxCapacity: number): Promise<void> {
    await this.client.set(`MaxCapacity#${groupName}`, maxCapacity.toString());
  }

  async getUserSocketId(userId: string): Promise<string | null> {
    return await this.client.get(userId);
  }

  async setUserSocketId(userId: string, socketId: string): Promise<void> {
    await this.client.set(userId, socketId);
  }
}

type Message = {
  from: string;
  to: string;
};

type CreateGroupResponse = {
  status: number;
  message: string;
};

type ListGroupsResponse = {
  groups: Group[];
};

type Group = {
  id: string;
  name: string;
};

const log = (logMessage: string | object) => {
  if (isLogging) {
    console.log(logMessage);
  }
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
  if (isLogging) {
    console.log(`Start on port ${apiPort}`);
  }
});
const subClient = pubClient.duplicate();
subClient.on("error", (err) => {
  if (isLogging) {
    console.log("Redis subClient Error", err);
  }
});
io.adapter(createAdapter(pubClient, subClient)); // redis-adapter

const activeGroups = (): Map<string, Set<string>> => {
  // @ts-ignore See https://socket.io/docs/v4/rooms/#implementation-details
  return io.sockets.adapter.rooms;
};

io.on("connection", async (socket: Socket) => {
  let myGroupName = "";
  let myUserId = "";

  const getGroupList = async () => {
    return await redisClient.getGroupList();
  };

  socket.on(
    "list groups",
    async (callback: (response: ListGroupsResponse) => void) => {
      const groupList = await getGroupList();
      callback({
        groups: [...groupList].map((entry) => ({
          name: entry[0],
          id: entry[1],
        })),
      });
    }
  );

  socket.on(
    "create group",
    async (
      groupName: string,
      maxCapacity: number,
      callback: (response: CreateGroupResponse) => void
    ) => {
      const wrapper = (response: CreateGroupResponse) => {
        log(response);
        callback(response);
      };

      const groupList = await getGroupList();
      if (groupList.has(groupName)) {
        const message = `Group already exists. groupName: ${groupName}`;
        wrapper({ status: 409, message: message });
        return;
      }
      groupList.set(groupName, socket.id.toString());
      if (isLogging) {
        console.log(groupList);
        console.log(JSON.stringify(Object.fromEntries(groupList)));
      }
      await redisClient.setGroupList(groupList);

      if (maxCapacity) {
        await redisClient.setMaxCapacity(groupName, maxCapacity);
      }

      const message = `Group have been created. groupName: ${groupName}`;
      wrapper({ status: 200, message: message });
    }
  );

  socket.on(
    "delete group",
    async (groupName: string, callback: (response: number) => void) => {
      socket.to(groupName).emit("delete group");

      callback(200);
    }
  );

  socket.on(
    "join",
    async (
      userId: string,
      groupName: string,
      callback: (response: string) => void
    ) => {
      myGroupName = groupName;
      myUserId = userId;

      const maxCapacity = await redisClient.getMaxCapacity(groupName);
      if (maxCapacity) {
        const connectedClientNum = activeGroups().get(myGroupName)?.size as number;
        if (maxCapacity !== null && connectedClientNum >= maxCapacity) {
          if (isLogging) {
            console.log(`Reject user: ${myUserId}`);
          }
          callback("rejected");
          return;
        }
      }

      callback("approved");
      if (isLogging) {
        console.log(`join: userId=${myUserId}, groupName=${myGroupName}`);
      }
      await redisClient.setUserSocketId(userId, socket.id.toString());
      await socket.join(myGroupName);
      socket.to(myGroupName).emit("user joined", myUserId);
    }
  );

  socket.on("message", async (message: Message) => {
    message.from = myUserId;
    if (message.to) {
      const socketId = await redisClient.getUserSocketId(message.to);
      if (socketId) {
        socket.to(socketId).emit("message", message);
      }
      return;
    }
    if (myGroupName) {
      socket.to(myGroupName).emit("message", message);
    }
  });

  const handleDisconnect = async () => {
    if (myGroupName) {
      if (isLogging) {
        console.log(
          `user leaving: userId=${myUserId}, groupName=${myGroupName}`
        );
      }
      socket.to(myGroupName).emit("user leaving", myUserId);
      socket.leave(myGroupName);

      myGroupName = "";
    }
    const groupList = await getGroupList();
    groupList.forEach((_, key) => {
      const group = activeGroups().get(key);
      const groupUserNum = group ? group.size : 0;
      if (isLogging) {
        console.log(`group: ${key}`, `group size: ${groupUserNum}`)
      }
      if (groupUserNum === 0) {
        groupList.delete(key);
      }
    });
    
    await redisClient.setGroupList(groupList);
  };

  socket.on("leave", handleDisconnect);

  socket.on("disconnect", () => {
    if (isLogging) {
      console.log("disconnect");
    }
    handleDisconnect();
  });

  const redisClient = new RedisClient(redisUrl);

  await redisClient.connect();
  if (isLogging) {
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
    if (isLogging) {
      console.log(`Socket.io Listen: ${appPort}`);
      console.log(
        "=================================Restarted======================================"
      );
    }
  });
