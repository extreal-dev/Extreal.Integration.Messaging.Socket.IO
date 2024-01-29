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

  async getClientSocketId(clientId: string): Promise<string | null> {
    return await this.client.get(clientId);
  }

  async setClientSocketId(clientId: string, socketId: string): Promise<void> {
    await this.client.set(clientId, socketId);
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

const getGroups = (): Map<string, string> => {
  // @ts-ignore See https://socket.io/docs/v4/rooms/#implementation-details
  const rooms = io.of("/").adapter.rooms;
  const groups = new Map<string, string>();

  rooms.forEach((members: Set<string>, roomName: string) => {
    const isDefaultRoom = members.has(roomName);
    if (!isDefaultRoom) {
      const firstMemberId = [...members][0];
      groups.set(roomName, firstMemberId);
    }
  });

  return groups;
};

const getGroupsMembers = (): Map<string, string[]> => {
  const rooms = io.of("/").adapter.rooms;
  const groupsMembers = new Map<string, string[]>();

  rooms.forEach((members: Set<string>, roomName: string) => {
    const isDefaultRoom = members.has(roomName);
    if (!isDefaultRoom) {
      groupsMembers.set(roomName, [...members]);
    }
  });

  return groupsMembers;
};

io.on("connection", async (socket: Socket) => {
  let myGroupName = "";
  let myClientId = "";
  let myGroupMaxCapacity = 0;

  socket.on(
    "list groups",
    async (callback: (response: ListGroupsResponse) => void) => {
      const groups = getGroups();
            callback({
        groups: [...groups].map((entry) => ({
          name: entry[0],
          id: entry[1],
        })),
      });
    }
  );

  socket.on(
    "join",
    async (
      clientId: string,
      groupName: string,
      maxCapacity: number,
      callback: (response: string) => void
    ) => {
      myGroupName = groupName;
      myClientId = clientId;
      myGroupMaxCapacity = maxCapacity;

      if (maxCapacity) {
        await redisClient.setMaxCapacity(groupName, myGroupMaxCapacity);
      }

      const groupMaxCapacity = await redisClient.getMaxCapacity(groupName);
      if (groupMaxCapacity) {
        const connectedClientNum = getGroupsMembers().get(myGroupName)?.length as number;
        if (groupMaxCapacity !== null && connectedClientNum >= groupMaxCapacity) {
          log(`Reject client: ${myClientId}`);
          callback("rejected");
          return;
        }
      }

      callback("approved");
      log(`join: clientId=${myClientId}, groupName=${myGroupName}`);     
      await redisClient.setClientSocketId(clientId, socket.id.toString());
      await socket.join(myGroupName);
      socket.to(myGroupName).emit("client joined", myClientId);
    }
  );

  socket.on("message", async (message: Message) => {
    message.from = myClientId;
    if (message.to) {
      const socketId = await redisClient.getClientSocketId(message.to);
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
      log(`client leaving: clientId=${myClientId}, groupName=${myGroupName}`);
      socket.to(myGroupName).emit("client leaving", myClientId);
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

