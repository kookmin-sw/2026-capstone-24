package com.murang.room.runtime;

public record RoomTaskStopRequest(
        Long roomId,
        String clusterArn,
        String taskArn,
        String reason
) {
}
