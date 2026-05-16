package com.murang.room.runtime;

public record RoomTaskRuntimeState(
        String lastStatus,
        boolean running
) {
}
