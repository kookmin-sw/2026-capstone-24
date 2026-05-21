package com.murang.room.manager;

import com.murang.room.config.RoomReconciliationProperties;
import com.murang.room.domain.RoomServerInstanceStatus;
import com.murang.room.repository.RoomServerInstanceRepository;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

@Component
public class RoomReconciliationScheduler {

    private static final Logger log = LoggerFactory.getLogger(RoomReconciliationScheduler.class);

    private final RoomServerManager manager;
    private final RoomServerInstanceRepository instanceRepository;
    private final RoomReconciliationProperties properties;
    private final Clock clock;

    public RoomReconciliationScheduler(RoomServerManager manager,
            RoomServerInstanceRepository instanceRepository,
            RoomReconciliationProperties properties,
            Clock clock) {
        this.manager = manager;
        this.instanceRepository = instanceRepository;
        this.properties = properties;
        this.clock = clock;
    }

    @Scheduled(fixedDelayString = "${murang.room.reconciliation.scan-interval-ms:30000}")
    public void scan() {
        Instant now = Instant.now(clock);

        // 1. heartbeat timeout: READY / ACTIVE
        Instant heartbeatThreshold = now.minus(properties.heartbeatTimeout());
        List<Long> heartbeatExpired = instanceRepository
                .findAllByStatusInAndLastHeartbeatAtBefore(
                        List.of(RoomServerInstanceStatus.READY, RoomServerInstanceStatus.ACTIVE),
                        heartbeatThreshold)
                .stream().map(i -> i.getRoomId()).toList();

        for (Long roomId : heartbeatExpired) {
            try {
                log.info("reconciliation: heartbeat-timeout room_id={}", roomId);
                manager.markUnhealthyAndTerminate(roomId, "reconciliation:heartbeat-timeout");
            } catch (Exception ex) {
                log.error("reconciliation: failed to terminate room_id={}", roomId, ex);
            }
        }

        // 2. provisioning timeout: PROVISIONING / SERVER_STARTING
        Instant provisioningThreshold = now.minus(properties.provisioningTimeout());
        List<Long> provisioningExpired = instanceRepository
                .findAllByStatusIn(List.of(RoomServerInstanceStatus.PROVISIONING, RoomServerInstanceStatus.SERVER_STARTING))
                .stream()
                .filter(i -> i.getCreatedAt().isBefore(provisioningThreshold))
                .map(i -> i.getRoomId())
                .toList();

        for (Long roomId : provisioningExpired) {
            try {
                log.info("reconciliation: provisioning-timeout room_id={}", roomId);
                manager.markProvisioningTimedOut(roomId, "reconciliation:provisioning-timeout");
            } catch (Exception ex) {
                log.error("reconciliation: failed to mark provisioning-timeout room_id={}", roomId, ex);
            }
        }
    }
}
