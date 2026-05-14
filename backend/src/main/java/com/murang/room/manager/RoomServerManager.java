package com.murang.room.manager;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

public interface RoomServerManager {

    RoomServerSnapshot provision(RoomProvisioningCommand command);

    void notifyReady(Long roomId, RoomReadySignal signal);

    void notifyHeartbeat(Long roomId, Instant receivedAt);

    void markActive(Long roomId);

    void terminate(Long roomId, String reason);

    Optional<RoomServerSnapshot> findByRoomId(Long roomId);

    List<RoomServerSnapshot> findAdmissionOpen();
}
