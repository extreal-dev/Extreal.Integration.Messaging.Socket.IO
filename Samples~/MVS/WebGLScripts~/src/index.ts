import { RedisMessagingTransportAdapter } from "@extreal-dev/extreal.integration.messaging.redis";
import { addFunction, isDebug } from "@extreal-dev/extreal.integration.web.common";
import { isTouchDevice } from "./isTouchDevice";

const redisMessagingTransportAdapter = new RedisMessagingTransportAdapter();
redisMessagingTransportAdapter.adapt();

addFunction("IsTouchDevice", () => {
    const result = isTouchDevice();
    if (isDebug) {
        console.log(`call isTouchDevice: ${result}`);
    }
    return result.toString();
});