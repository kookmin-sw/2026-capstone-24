package com.murang.room.manager;

public record RoomReadySignal(
        String taskPublicIp,
        int gamePort,
        String roomRuntimeVersion
) {
}
