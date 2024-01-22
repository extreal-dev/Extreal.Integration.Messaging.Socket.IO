import { serve } from "https://deno.land/std@0.212.0/http/server.ts";
import { createRedisAdapter, createRedisClient, Server, Socket } from "https://deno.land/x/socket_io@0.2.0/mod.ts";
import { connect, Redis } from "https://deno.land/x/redis@v0.32.1/mod.ts";

const appPort = 3030;
const redisHost = "messaging-redis";
const redisPort = 6379;
const isLogging = Deno.env.get("MESSAGING_LOGGING")?.toLowerCase() === "on";

class RedisClient {
  private hostname: string;
  private port: number;
  private client: Redis;

  constructor(hostname: string, port: number) {
    this.hostname = hostname;
    this.port = port;
  }

  async connect() {
    this.client = await connect({hostname: this.hostname, port: this.port});
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

type ListGroupsResponse = {
  groups: Group[];
};

type Group = {
  id: string;
  name: string;
};

const log = (...logMessages: (string | object)[]) => {
  if (isLogging) {
    logMessages.forEach(logMessage => {
      console.log(logMessage);
    });
  }
};

const corsConfig = {
  origin: Deno.env.get("MESSAGING_CORS_ORIGIN"),
};

const [pubClient, subClient] = await Promise.all([
    createRedisClient({
        hostname: redisHost,
    }),
    createRedisClient({
        hostname: redisHost,
    }),
  ]);

const io = new Server( {
  cors: corsConfig,
  adapter: createRedisAdapter(pubClient, subClient),
});

const getGroupsInfo = (): { groupList: Map<string, string>, groupMembers: Map<string, string[]> } => {
  // @ts-ignore See https://socket.io/docs/v4/rooms/#implementation-details
  const groupsMap = io.of("/").adapter.rooms;
  const groupList = new Map<string, string>();
  const groupMembers = new Map<string, string[]>();

  groupsMap.forEach((members: Set<string>, roomName: string) => {
    const isDefaultRoom = members.has(roomName);
    if (!isDefaultRoom) {
      const firstMemberId = [...members][0];
      groupList.set(roomName, firstMemberId);
      groupMembers.set(roomName, [...members]);
    }
  });

  return { groupList, groupMembers };
};

io.on("connection", async (socket: Socket) => {
  let myGroupName = "";
  let myUserId = "";
  let myGroupMaxCapacity = 0;

  socket.on(
    "list groups",
    async (callback: (response: ListGroupsResponse) => void) => {
      const groupList = getGroupsInfo().groupList;
            callback({
        groups: [...groupList].map((entry) => ({
          name: entry[0],
          id: entry[1],
        })),
      });
    }
  );

  socket.on(
    "join",
    async (
      userId: string,
      groupName: string,
      maxCapacity: number,
      callback: (response: string) => void
    ) => {
      myGroupName = groupName;
      myUserId = userId;
      myGroupMaxCapacity = maxCapacity;

      if (maxCapacity) {
        await redisClient.setMaxCapacity(groupName, myGroupMaxCapacity);
      }

      const groupMaxCapacity = await redisClient.getMaxCapacity(groupName);
      if (groupMaxCapacity) {
        const connectedClientNum = getGroupsInfo().groupMembers.get(myGroupName)?.length as number;
        if (groupMaxCapacity !== null && connectedClientNum >= groupMaxCapacity) {
          log(`Reject user: ${myUserId}`);
          callback("rejected");
          return;
        }
      }

      callback("approved");
      log(`join: userId=${myUserId}, groupName=${myGroupName}`);     
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
      log(`msg received: userId=${message.from}, groupName=${myGroupName}`);
    }
  });

  const handleDisconnect = async () => {
    if (myGroupName) {
      log(`user leaving: userId=${myUserId}, groupName=${myGroupName}`);
      socket.to(myGroupName).emit("user leaving", myUserId);
      socket.leave(myGroupName);
      myGroupName = "";
    }
  };

  socket.on("leave", handleDisconnect);

  socket.on("disconnect", () => {
    log(`client disconnected: socket id=${socket.id}`);
    handleDisconnect();
  });

    log(`client connected: socket id=${socket.id}`);

});
  const redisClient = new RedisClient(redisHost, redisPort);
  await redisClient.connect();
  log("=================================Restarted======================================");
  await serve(io.handler(), { port: appPort, });

