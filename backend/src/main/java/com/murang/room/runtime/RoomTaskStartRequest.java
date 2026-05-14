package com.murang.room.runtime;

import java.net.URI;

public record RoomTaskStartRequest(
        Long roomId,
        String photonSessionName,
        int maxPlayers,
        String roomRuntimeVersion,
        URI readyCallbackUrl,
        URI heartbeatCallbackUrl
) {
}
