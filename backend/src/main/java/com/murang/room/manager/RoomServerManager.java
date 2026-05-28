package com.murang.room.manager;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

public interface RoomServerManager {

    RoomServerSnapshot provision(RoomProvisioningCommand command);

    RoomServerSnapshot provisionPersistent(RoomProvisioningCommand command);

    void notifyReady(Long roomId, RoomReadySignal signal);

    void notifyHeartbeat(Long roomId, Instant receivedAt);

    void markActive(Long roomId);

    void terminate(Long roomId, String reason);

    /**
     * Reconciliation helper: transitions READY/ACTIVE instance to UNHEALTHY then runs
     * the full terminate flow (stopRoomTask + markTerminated + room.close).
     * No-op if instance is already TERMINATED or FAILED.
     */
    void markUnhealthyAndTerminate(Long roomId, String reason);

    /**
     * Reconciliation helper: marks a PROVISIONING/SERVER_STARTING instance as FAILED
     * and attempts to stop the ECS task. No-op if already TERMINATED or FAILED.
     */
    void markProvisioningTimedOut(Long roomId, String reason);

    Optional<RoomServerSnapshot> findByRoomId(Long roomId);

    List<RoomServerSnapshot> findAdmissionOpen();
}
