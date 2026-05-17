package com.murang.room.manager;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.murang.common.exception.ApiException;
import com.murang.common.exception.ErrorCode;
import com.murang.room.domain.RoomServerInstanceStatus;
import com.murang.room.runtime.NoopRoomRuntimeProvider;
import com.murang.room.runtime.ProvisionedRoomTask;
import com.murang.room.runtime.RoomRuntimeProvider;
import com.murang.room.runtime.RoomTaskRuntimeState;
import com.murang.room.runtime.RoomTaskStartRequest;
import com.murang.room.runtime.RoomTaskStopRequest;
import com.murang.user.domain.UserAccount;
import com.murang.user.repository.UserAccountRepository;
import java.time.Instant;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Primary;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.transaction.annotation.Transactional;

@SpringBootTest
@ActiveProfiles("test")
class RoomServerManagerImplTest {

    @TestConfiguration
    static class TestRuntimeConfig {

        @Bean
        @Primary
        RoomRuntimeProvider stubRuntime() {
            return new StubRoomRuntimeProvider();
        }
    }

    @Autowired
    private RoomServerManager manager;

    @Autowired
    private RoomRuntimeProvider runtimeProvider;

    @Autowired
    private UserAccountRepository userAccountRepository;

    @BeforeEach
    void resetStub() {
        ((StubRoomRuntimeProvider) runtimeProvider).reset();
    }

    @Test
    @Transactional
    void provision_persistsRoomAndCallsRuntime() {
        Long ownerUserId = createOwner();

        RoomServerSnapshot snapshot = manager.provision(new RoomProvisioningCommand(
                ownerUserId, uniqueSessionName(), 8, null, "v0.1.0"));

        assertThat(snapshot.roomId()).isNotNull();
        assertThat(snapshot.ownerUserId()).isEqualTo(ownerUserId);
        assertThat(snapshot.maxPlayers()).isEqualTo(8);
        assertThat(snapshot.status()).isEqualTo(RoomServerInstanceStatus.SERVER_STARTING);
        assertThat(snapshot.ecsClusterArn()).isEqualTo("stub:cluster");
        assertThat(snapshot.ecsTaskArn()).startsWith("stub:task-");

        StubRoomRuntimeProvider stub = (StubRoomRuntimeProvider) runtimeProvider;
        assertThat(stub.lastStart).isNotNull();
        assertThat(stub.lastStart.roomId()).isEqualTo(snapshot.roomId());
        assertThat(stub.lastStart.readyCallbackUrl().toString()).contains("/internal/rooms/");
        assertThat(stub.lastStart.readyCallbackUrl().toString()).endsWith("/ready");
    }

    @Test
    @Transactional
    void notifyReady_transitionsInstanceToReady() {
        RoomServerSnapshot provisioned = manager.provision(new RoomProvisioningCommand(
                createOwner(), uniqueSessionName(), 4, null, "v0.1.0"));

        manager.notifyReady(provisioned.roomId(), new RoomReadySignal(
                "203.0.113.10", 7777, "v0.1.0"));

        Optional<RoomServerSnapshot> after = manager.findByRoomId(provisioned.roomId());
        assertThat(after).isPresent();
        assertThat(after.get().status()).isEqualTo(RoomServerInstanceStatus.READY);
        assertThat(after.get().taskPublicIp()).isEqualTo("203.0.113.10");
        assertThat(after.get().gamePort()).isEqualTo(7777);
        assertThat(after.get().readyAt()).isNotNull();
    }

    @Test
    @Transactional
    void notifyHeartbeat_updatesLastHeartbeatAt() {
        RoomServerSnapshot provisioned = manager.provision(new RoomProvisioningCommand(
                createOwner(), uniqueSessionName(), 4, null, "v0.1.0"));
        manager.notifyReady(provisioned.roomId(), new RoomReadySignal("203.0.113.10", 7777, "v0.1.0"));

        Instant beat = Instant.parse("2026-05-14T01:00:00Z");
        manager.notifyHeartbeat(provisioned.roomId(), beat);

        RoomServerSnapshot after = manager.findByRoomId(provisioned.roomId()).orElseThrow();
        assertThat(after.lastHeartbeatAt()).isEqualTo(beat);
    }

    @Test
    @Transactional
    void terminate_callsRuntimeStopAndClosesRoom() {
        RoomServerSnapshot provisioned = manager.provision(new RoomProvisioningCommand(
                createOwner(), uniqueSessionName(), 4, null, "v0.1.0"));
        manager.notifyReady(provisioned.roomId(), new RoomReadySignal("203.0.113.10", 7777, "v0.1.0"));

        manager.terminate(provisioned.roomId(), "last user left");

        StubRoomRuntimeProvider stub = (StubRoomRuntimeProvider) runtimeProvider;
        assertThat(stub.lastStop).isNotNull();
        assertThat(stub.lastStop.roomId()).isEqualTo(provisioned.roomId());
        assertThat(stub.lastStop.reason()).isEqualTo("last user left");

        RoomServerSnapshot after = manager.findByRoomId(provisioned.roomId()).orElseThrow();
        assertThat(after.status()).isEqualTo(RoomServerInstanceStatus.TERMINATED);
        assertThat(after.terminatedAt()).isNotNull();
        assertThat(after.closedAt()).isNotNull();
    }

    @Test
    @Transactional
    void notifyReady_unknownRoom_throwsRoomNotFound() {
        assertThatThrownBy(() -> manager.notifyReady(9999L, new RoomReadySignal("ip", 7777, "v")))
                .isInstanceOf(ApiException.class)
                .extracting(ex -> ((ApiException) ex).getErrorCode())
                .isEqualTo(ErrorCode.ROOM_NOT_FOUND);
    }

    @Test
    @Transactional
    void findAdmissionOpen_returnsOnlyReadyOrActiveAndNotClosed() {
        Long owner = createOwner();
        RoomServerSnapshot openRoom = manager.provision(new RoomProvisioningCommand(
                owner, uniqueSessionName(), 4, null, "v0.1.0"));
        manager.notifyReady(openRoom.roomId(), new RoomReadySignal("ip", 7777, "v0.1.0"));

        RoomServerSnapshot terminated = manager.provision(new RoomProvisioningCommand(
                owner, uniqueSessionName(), 4, null, "v0.1.0"));
        manager.notifyReady(terminated.roomId(), new RoomReadySignal("ip", 7777, "v0.1.0"));
        manager.terminate(terminated.roomId(), "cleanup");

        assertThat(manager.findAdmissionOpen())
                .extracting(RoomServerSnapshot::roomId)
                .containsExactly(openRoom.roomId());
    }

    @Test
    @Transactional
    void provision_runtimeFailure_marksFailedAndThrows() {
        StubRoomRuntimeProvider stub = (StubRoomRuntimeProvider) runtimeProvider;
        stub.failNext = true;

        assertThatThrownBy(() -> manager.provision(new RoomProvisioningCommand(
                createOwner(), uniqueSessionName(), 4, null, "v0.1.0")))
                .isInstanceOf(ApiException.class)
                .extracting(ex -> ((ApiException) ex).getErrorCode())
                .isEqualTo(ErrorCode.ROOM_PROVISIONING_FAILED);
    }

    private Long createOwner() {
        String suffix = UUID.randomUUID().toString().substring(0, 8);
        UserAccount user = UserAccount.create(
                "meta-test-" + suffix,
                null,
                "Owner-" + suffix,
                Instant.now()
        );
        return userAccountRepository.save(user).getUserId();
    }

    private String uniqueSessionName() {
        return "murang-room-" + UUID.randomUUID().toString().substring(0, 8);
    }

    static class StubRoomRuntimeProvider extends NoopRoomRuntimeProvider {
        RoomTaskStartRequest lastStart;
        RoomTaskStopRequest lastStop;
        int counter;
        boolean failNext;

        void reset() {
            lastStart = null;
            lastStop = null;
            counter = 0;
            failNext = false;
        }

        @Override
        public ProvisionedRoomTask startRoomTask(RoomTaskStartRequest request) {
            if (failNext) {
                failNext = false;
                throw new RuntimeException("simulated runtime provider failure");
            }
            lastStart = request;
            counter++;
            return new ProvisionedRoomTask("stub:cluster", "stub:task-" + counter);
        }

        @Override
        public void stopRoomTask(RoomTaskStopRequest request) {
            lastStop = request;
        }

        @Override
        public Optional<RoomTaskRuntimeState> describeRoomTask(String clusterArn, String taskArn) {
            return Optional.empty();
        }
    }

    @Test
    @Transactional
    void markUnhealthyAndTerminate_persistsUnhealthyTransitionAndCallsStopTask() {
        RoomServerSnapshot provisioned = manager.provision(new RoomProvisioningCommand(
                createOwner(), uniqueSessionName(), 4, null, "v0.1.0"));
        manager.notifyReady(provisioned.roomId(), new RoomReadySignal("10.0.0.1", 7777, "v0.1.0"));
        manager.markUnhealthyAndTerminate(provisioned.roomId(), "reconciliation:heartbeat-timeout");
        StubRoomRuntimeProvider stub = (StubRoomRuntimeProvider) runtimeProvider;
        assertThat(stub.lastStop).isNotNull();
        assertThat(stub.lastStop.reason()).isEqualTo("reconciliation:heartbeat-timeout");
        RoomServerSnapshot after = manager.findByRoomId(provisioned.roomId()).orElseThrow();
        assertThat(after.status()).isEqualTo(RoomServerInstanceStatus.TERMINATED);
        assertThat(after.terminatedAt()).isNotNull();
        assertThat(after.closedAt()).isNotNull();
    }

    @Test
    @Transactional
    void markUnhealthyAndTerminate_isIdempotentForAlreadyTerminated() {
        RoomServerSnapshot provisioned = manager.provision(new RoomProvisioningCommand(
                createOwner(), uniqueSessionName(), 4, null, "v0.1.0"));
        manager.notifyReady(provisioned.roomId(), new RoomReadySignal("10.0.0.1", 7777, "v0.1.0"));
        manager.terminate(provisioned.roomId(), "first-terminate");
        StubRoomRuntimeProvider stub = (StubRoomRuntimeProvider) runtimeProvider;
        stub.lastStop = null;
        manager.markUnhealthyAndTerminate(provisioned.roomId(), "reconciliation:heartbeat-timeout");
        assertThat(stub.lastStop).isNull();
    }

    @Test
    @Transactional
    void markProvisioningTimedOut_marksFailedAndAttemptsStopTask() {
        RoomServerSnapshot provisioned = manager.provision(new RoomProvisioningCommand(
                createOwner(), uniqueSessionName(), 4, null, "v0.1.0"));
        manager.markProvisioningTimedOut(provisioned.roomId(), "reconciliation:provisioning-timeout");
        StubRoomRuntimeProvider stub = (StubRoomRuntimeProvider) runtimeProvider;
        assertThat(stub.lastStop).isNotNull();
        RoomServerSnapshot after = manager.findByRoomId(provisioned.roomId()).orElseThrow();
        assertThat(after.status()).isEqualTo(RoomServerInstanceStatus.FAILED);
        assertThat(after.terminatedAt()).isNotNull();
        assertThat(after.closedAt()).isNotNull();
    }
}
