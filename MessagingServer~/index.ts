import { createRedisAdapter, createRedisClient, Server, Socket } from "https://deno.land/x/socket_io@0.2.0/mod.ts";

const appPort = 3030;
const redisHost = "messaging-redis";
const isLogging = Deno.env.get("MESSAGING_LOGGING")?.toLowerCase() === "on";
const maxCapacity = parseInt(Deno.env.get("MESSAGING_MAX_CAPACITY")) || 100;

type Message = {
    from: string;
    to: string;
};

type ListGroupsResponse = {
    groups: Group[];
};

type Group = {
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
    return adapter.rooms;
};

const getGroups = () : string[] => {
    return [...rooms().entries()]
        .filter((entry) => !entry[1].has(entry[0]))
        .map((entry) => (entry[0]))
};

io.on("connection", async (socket: Socket) => {
    socket.on(
        "list groups",
        async (callback: (response: ListGroupsResponse) => void) => {
            const wrapper = (response: ListGroupsResponse) => {
                log(() => response);
                callback(response);
            };

            io.serverSideEmit("list server groups", (error, responses: string[][]) => {
                if (error && isLogging) {
                    console.log(error);
                }
                const groups: Set<string> = new Set();

                const localGroups = getGroups();
                localGroups.forEach(localRoom => groups.add(localRoom));

                responses.forEach(response => {
                    response.forEach(group => {
                        groups.add(group);
                    });
                });

                wrapper({groups: [...groups].map(room => ({ name: room }))})
            });
        }
    );

    socket.on(
        "join",
        async (groupName: string, callback: (response: string) => void) => {
            const clients = rooms().get(groupName);
            const isCapacityOver = clients && clients.size >= maxCapacity;
            if (isCapacityOver) {
                log(() => `Reject client: ${socket.id}`);
                callback("rejected");
                return;
            }
            
            callback("approved");
            log(() => `join: clientId=${socket.id}, groupName=${groupName}`);     
            await socket.join(groupName);
            socket.to(groupName).emit("client joined", socket.id);
        }
    );
    
    socket.on("message", async (message: Message) => {
        message.from = socket.id;
        if (message.to) {
            socket.to(message.to).emit("message", message);
            return;
        }
        socket.to([...socket.rooms]).emit("message", message);
    });

    const leave = async () => {
        for (const room of socket.rooms) {
            if (room === socket.id) {
                continue;
            }
            log(() => `client leaving: clientId=${socket.id}, groupName=${room}`);
            socket.to(room).emit("client leaving", socket.id);
            socket.leave(room);
        }
    };

    socket.on("leave", leave);

    socket.on("disconnect", () => {
        log(() => `client disconnected: socket id=${socket.id}`);
        leave();
    });

    log(() => `client connected: socket id=${socket.id}`);
});

io.on("list server groups", (callback: (rooms: string[]) => void) => {
    callback(getGroups());
});

log(() => "=================================Restarted======================================");
await Deno.serve({ port: appPort, }, io.handler());
