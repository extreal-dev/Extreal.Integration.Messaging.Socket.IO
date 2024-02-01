import { serve } from "https://deno.land/std@0.212.0/http/server.ts";
import { createRedisAdapter, createRedisClient, Server, Socket } from "https://deno.land/x/socket_io@0.2.0/mod.ts";

const appPort = 3030;
const redisHost = "messaging-redis";
const isLogging = Deno.env.get("MESSAGING_LOGGING")?.toLowerCase() === "on";
const maxCapacity = 100;

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

const log = (logMessage: () => string | object) => {
  if (isLogging) {
    console.log(logMessage());
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

const adapter = io.of("/").adapter;

const rooms = (): Map<string, Set<string>> => {
    // @ts-ignore See https://socket.io/docs/v4/rooms/#implementation-details
    return adapter.rooms;
};

io.on("connection", async (socket: Socket) => {
  let myGroupName = "";
  let myClientId = socket.id.toString();
  socket.on(
    "list groups",
    async (callback: (response: ListGroupsResponse) => void) => {
      const wrapper = (response: ListGroupsResponse) => {
        log(() => response);
        callback(response);
      };

      wrapper({
        groups: [...rooms().entries()]
              .filter((entry) => !entry[1].has(entry[0]))
              .map((entry) => ({ name: entry[0], id: [...entry[1]][0] })),
      });
    }
  );

  socket.on(
    "join",
    async (groupName: string, callback: (response: string) => void) => {
      myGroupName = groupName;
      const connectedClientNum = [...rooms().entries()]
        .filter((entry) => entry[0] === myGroupName)
        .map((entry) => entry[1].size)[0] || 0;

      if (connectedClientNum >= maxCapacity) {
        log(() => `Reject client: ${myClientId}`);
        callback("rejected");
        return;
      }
      
      callback("approved");
      log(() => `join: clientId=${myClientId}, groupName=${myGroupName}`);     
      await socket.join(myGroupName);
      socket.to(myGroupName).emit("client joined", myClientId);
    }
  );
  
  socket.on("message", async (message: Message) => {
    message.from = myClientId;
    if (message.to) {
      socket.to(message.to).emit("message", message);
      return;
    }
    if (myGroupName) {
      socket.to(myGroupName).emit("message", message);
    }
  });

  const leave = async () => {
    if (myGroupName) {
      log(() => `client leaving: clientId=${myClientId}, groupName=${myGroupName}`);
      socket.to(myGroupName).emit("client leaving", myClientId);
      socket.leave(myGroupName);
      myGroupName = "";
    }
  };

  socket.on("leave", leave);

  socket.on("disconnect", () => {
    log(() => `client disconnected: socket id=${myClientId}`);
    leave();
  });

    log(() => `client connected: socket id=${myClientId}`);

});
  log(() => "=================================Restarted======================================");
  await serve(io.handler(), { port: appPort, });

