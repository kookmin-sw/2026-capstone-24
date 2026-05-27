package com.murang.room.manager;

import com.murang.room.domain.Room;
import com.murang.room.domain.RoomServerInstance;
import com.murang.room.domain.RoomServerInstanceStatus;
import java.time.Instant;

public record RoomServerSnapshot(
        Long roomId,
        Long ownerUserId,
        String photonSessionName,
        int maxPlayers,
        boolean locked,
        Long instanceId,
        RoomServerInstanceStatus status,
        String ecsClusterArn,
        String ecsTaskArn,
        String taskPublicIp,
        Integer gamePort,
        Instant createdAt,
        Instant readyAt,
        Instant lastHeartbeatAt,
        Instant terminatedAt,
        Instant closedAt,
        boolean isPersistent
) {

    public static RoomServerSnapshot of(Room room, RoomServerInstance instance) {
        return new RoomServerSnapshot(
                room.getRoomId(),
                room.getOwnerUserId(),
                room.getPhotonSessionName(),
                room.getMaxPlayers(),
                room.isLocked(),
                instance.getId(),
                instance.getStatus(),
                instance.getEcsClusterArn(),
                instance.getEcsTaskArn(),
                instance.getTaskPublicIp(),
                instance.getGamePort(),
                instance.getCreatedAt(),
                instance.getReadyAt(),
                instance.getLastHeartbeatAt(),
                instance.getTerminatedAt(),
                room.getClosedAt(),
                room.isPersistent()
        );
    }

    public boolean admissionOpen() {
        return status == RoomServerInstanceStatus.READY || status == RoomServerInstanceStatus.ACTIVE;
    }
}
