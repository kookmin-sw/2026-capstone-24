package com.murang.room.manager;

public record RoomProvisioningCommand(
        Long ownerUserId,
        String photonSessionName,
        int maxPlayers,
        String passwordHash,
        String roomRuntimeVersion
) {
}
