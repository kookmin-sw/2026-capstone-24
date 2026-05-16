package com.murang.room.domain;

import static org.assertj.core.api.Assertions.assertThat;

import java.time.Instant;
import org.junit.jupiter.api.Test;

class RoomServerInstanceTest {

    private static final Long ROOM_ID = 42L;
    private static final Instant NOW = Instant.parse("2026-05-14T00:00:00Z");

    @Test
    void provision_initializesAsProvisioning() {
        RoomServerInstance instance = RoomServerInstance.provision(ROOM_ID, NOW);

        assertThat(instance.getStatus()).isEqualTo(RoomServerInstanceStatus.PROVISIONING);
        assertThat(instance.getRoomId()).isEqualTo(ROOM_ID);
        assertThat(instance.getCreatedAt()).isEqualTo(NOW);
        assertThat(instance.getEcsTaskArn()).isNull();
        assertThat(instance.getReadyAt()).isNull();
    }

    @Test
    void attachEcsTask_transitionsToServerStarting() {
        RoomServerInstance instance = RoomServerInstance.provision(ROOM_ID, NOW);

        instance.attachEcsTask("arn:cluster", "arn:task:abc");

        assertThat(instance.getStatus()).isEqualTo(RoomServerInstanceStatus.SERVER_STARTING);
        assertThat(instance.getEcsClusterArn()).isEqualTo("arn:cluster");
        assertThat(instance.getEcsTaskArn()).isEqualTo("arn:task:abc");
    }

    @Test
    void markReady_setsAddressPortAndReadyTimestamp() {
        RoomServerInstance instance = RoomServerInstance.provision(ROOM_ID, NOW);
        instance.attachEcsTask("arn:cluster", "arn:task:abc");

        Instant readyAt = NOW.plusSeconds(30);
        instance.markReady("203.0.113.10", 7777, readyAt);

        assertThat(instance.getStatus()).isEqualTo(RoomServerInstanceStatus.READY);
        assertThat(instance.getTaskPublicIp()).isEqualTo("203.0.113.10");
        assertThat(instance.getGamePort()).isEqualTo(7777);
        assertThat(instance.getReadyAt()).isEqualTo(readyAt);
        assertThat(instance.getLastHeartbeatAt()).isEqualTo(readyAt);
    }

    @Test
    void markActive_onlyFromReady() {
        RoomServerInstance instance = RoomServerInstance.provision(ROOM_ID, NOW);

        instance.markActive();
        assertThat(instance.getStatus())
                .as("markActive from PROVISIONING is a no-op")
                .isEqualTo(RoomServerInstanceStatus.PROVISIONING);

        instance.attachEcsTask("arn:cluster", "arn:task:abc");
        instance.markReady("203.0.113.10", 7777, NOW);
        instance.markActive();
        assertThat(instance.getStatus()).isEqualTo(RoomServerInstanceStatus.ACTIVE);
    }

    @Test
    void recordHeartbeat_updatesTimestamp() {
        RoomServerInstance instance = RoomServerInstance.provision(ROOM_ID, NOW);
        instance.attachEcsTask("arn:cluster", "arn:task:abc");
        instance.markReady("203.0.113.10", 7777, NOW);

        Instant later = NOW.plusSeconds(120);
        instance.recordHeartbeat(later);

        assertThat(instance.getLastHeartbeatAt()).isEqualTo(later);
    }

    @Test
    void markUnhealthy_onlyFromReadyOrActive() {
        RoomServerInstance instance = RoomServerInstance.provision(ROOM_ID, NOW);

        instance.markUnhealthy();
        assertThat(instance.getStatus())
                .as("markUnhealthy from PROVISIONING is a no-op")
                .isEqualTo(RoomServerInstanceStatus.PROVISIONING);

        instance.attachEcsTask("arn:cluster", "arn:task:abc");
        instance.markReady("203.0.113.10", 7777, NOW);
        instance.markUnhealthy();
        assertThat(instance.getStatus()).isEqualTo(RoomServerInstanceStatus.UNHEALTHY);
    }

    @Test
    void beginTermination_thenMarkTerminated_setsTerminatedAt() {
        RoomServerInstance instance = RoomServerInstance.provision(ROOM_ID, NOW);
        instance.attachEcsTask("arn:cluster", "arn:task:abc");
        instance.markReady("203.0.113.10", 7777, NOW);
        instance.markActive();

        instance.beginTermination();
        assertThat(instance.getStatus()).isEqualTo(RoomServerInstanceStatus.TERMINATING);

        Instant terminatedAt = NOW.plusSeconds(600);
        instance.markTerminated(terminatedAt);
        assertThat(instance.getStatus()).isEqualTo(RoomServerInstanceStatus.TERMINATED);
        assertThat(instance.getTerminatedAt()).isEqualTo(terminatedAt);
    }

    @Test
    void markFailed_setsTerminatedAtAndStatus() {
        RoomServerInstance instance = RoomServerInstance.provision(ROOM_ID, NOW);

        Instant failedAt = NOW.plusSeconds(5);
        instance.markFailed(failedAt);

        assertThat(instance.getStatus()).isEqualTo(RoomServerInstanceStatus.FAILED);
        assertThat(instance.getTerminatedAt()).isEqualTo(failedAt);
    }
}
