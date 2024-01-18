import { serve } from "https://deno.land/std@0.192.0/http/server.ts";
import { createRedisAdapter, createRedisClient, Server, Socket } from "https://deno.land/x/socket_io@0.2.0/mod.ts";
import { createClient, RedisClientType } from "npm:redis@^4.5";
const appPort = 3030;
const redisHost = "messaging-redis";
const redisPort = 6379;
const isLogging = Deno.env.get("MESSAGING_LOGGING")?.toLowerCase() === "on";

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
    const activeGroups = await this.client.get("GroupList");
    if (activeGroups) {
      return new Map<string, string>(Object.entries(JSON.parse(activeGroups)));
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

const corsConfig = {
  origin: Deno.env.get("MESSAGING_CORS_ORIGIN"),
};

const [pubClient, subClient] = await Promise.all([
  createRedisClient({
      hostname: redisHost,
      port: 6379
  }),
  createRedisClient({
      hostname: redisHost,
      port: 6379
  }),
]);

const io = new Server( {
  cors: corsConfig,
  adapter: createRedisAdapter(pubClient, subClient),
});

const adapter = io.of("/").adapter;

const redisUrl = `redis://${redisHost}:${redisPort}`;

const activeGroups = (): Map<string, Set<string>> => {
  // @ts-ignore See https://socket.io/docs/v4/rooms/#implementation-details
  return adapter.rooms;
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

try {
  await serve(io.handler(), {
    port: appPort,
  });
  console.log(
    "=================================Restarted======================================"
  );
} catch (error) {
  console.error('start server error:', error);
}

