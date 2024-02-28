import { Server, Socket } from "https://deno.land/x/socket_io@0.2.0/mod.ts";

const appPort = 3030;
const isLogging = Deno.env.get("MESSAGING_LOGGING")?.toLowerCase() === "on";
const maxCapacity = parseInt(Deno.env.get("MESSAGING_MAX_CAPACITY")) || 100;

type Message = {
    from: string;
    to: string;
    messageContent: string;
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

const io = new Server( {
    cors: corsConfig,
});

const adapter = io.of("/").adapter;

const rooms = (): Map<string, Set<string>> => {
    return adapter.rooms;
};

io.on("connection", async (socket: Socket) => {
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
                    .map((entry) => ({ name: entry[0] })),
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

            log(() => `join: clientId=${socket.id}, groupName=${groupName}`);
            await socket.join(groupName);
            callback("approved");
            const members = rooms().get(groupName);
            if (members) {
                for (const member of members) {
                    if (member === socket.id) {
                        continue;
                    }
                    socket.to(member).emit("client joined", socket.id);
                    socket.emit("client joined", member);
                }
            }
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
log(() => "=================================Restarted======================================");
await Deno.serve({ port: appPort, }, io.handler());
