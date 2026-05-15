package com.murang.room.manager;

public record RoomReadySignal(
        String taskPublicIp,
        Integer gamePort,
        String roomRuntimeVersion
) {
}
