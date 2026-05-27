package com.murang.room.manager;

import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.murang.room.config.RoomReconciliationProperties;
import com.murang.room.domain.Room;
import com.murang.room.domain.RoomServerInstance;
import com.murang.room.domain.RoomServerInstanceStatus;
import com.murang.room.repository.RoomRepository;
import com.murang.room.repository.RoomServerInstanceRepository;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import org.assertj.core.api.Assertions;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentMatchers;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.context.ApplicationContext;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.util.ReflectionTestUtils;

@SpringBootTest
@ActiveProfiles("test")
class RoomReconciliationSchedulerTest {

    @Autowired
    private ApplicationContext applicationContext;
    @MockBean
    private RoomServerManager roomServerManager;

    @Test
    void schedulerBeanIsRegisteredInSpringContext() {
        RoomReconciliationScheduler s = applicationContext.getBean(RoomReconciliationScheduler.class);
        Assertions.assertThat(s).isNotNull();
    }

    @Test
    void scan_heartbeatExpired_callsMarkUnhealthyAndTerminateForExpiredOnly() {
        RoomServerInstanceRepository repo = mock(RoomServerInstanceRepository.class);
        RoomRepository roomRepo = mock(RoomRepository.class);
        RoomServerManager mgr = mock(RoomServerManager.class);
        Instant now = Instant.parse("2026-05-17T00:00:00Z");
        Clock fixedClock = Clock.fixed(now, ZoneOffset.UTC);
        RoomReconciliationProperties props = new RoomReconciliationProperties(
                Duration.ofSeconds(90), Duration.ofMinutes(5), Duration.ofSeconds(30));
        Instant threshold = now.minus(Duration.ofSeconds(90));
        RoomServerInstance r1 = fakeInstance(101L, RoomServerInstanceStatus.READY, now.minusSeconds(200), now.minusSeconds(100));
        RoomServerInstance r2 = fakeInstance(102L, RoomServerInstanceStatus.ACTIVE, now.minusSeconds(200), now.minusSeconds(100));
        when(repo.findAllByStatusInAndLastHeartbeatAtBefore(
                ArgumentMatchers.argThat(l -> l.contains(RoomServerInstanceStatus.READY)), eq(threshold)))
                .thenReturn(List.of(r1, r2));
        when(repo.findAllByStatusIn(ArgumentMatchers.argThat(l -> l.contains(RoomServerInstanceStatus.PROVISIONING))))
                .thenReturn(List.of());
        // 일반 룸(isPersistent=false)으로 mock 설정
        Room normalRoom1 = fakeRoom(101L, false);
        Room normalRoom2 = fakeRoom(102L, false);
        when(roomRepo.findAllById(ArgumentMatchers.anyIterable())).thenReturn(List.of(normalRoom1, normalRoom2));
        new RoomReconciliationScheduler(mgr, repo, roomRepo, props, fixedClock).scan();
        verify(mgr, times(1)).markUnhealthyAndTerminate(eq(101L), anyString());
        verify(mgr, times(1)).markUnhealthyAndTerminate(eq(102L), anyString());
    }

    @Test
    void scan_provisioningExpired_callsMarkProvisioningTimedOutForExpiredOnly() {
        RoomServerInstanceRepository repo = mock(RoomServerInstanceRepository.class);
        RoomRepository roomRepo = mock(RoomRepository.class);
        RoomServerManager mgr = mock(RoomServerManager.class);
        Instant now = Instant.parse("2026-05-17T00:00:00Z");
        Clock fixedClock = Clock.fixed(now, ZoneOffset.UTC);
        RoomReconciliationProperties props = new RoomReconciliationProperties(
                Duration.ofSeconds(90), Duration.ofMinutes(5), Duration.ofSeconds(30));
        RoomServerInstance p1 = fakeInstance(201L, RoomServerInstanceStatus.PROVISIONING, now.minusSeconds(400), null);
        RoomServerInstance p2 = fakeInstance(202L, RoomServerInstanceStatus.SERVER_STARTING, now.minusSeconds(60), null);
        when(repo.findAllByStatusInAndLastHeartbeatAtBefore(ArgumentMatchers.anyList(), ArgumentMatchers.any())).thenReturn(List.of());
        when(repo.findAllByStatusIn(ArgumentMatchers.argThat(l -> l.contains(RoomServerInstanceStatus.PROVISIONING))))
                .thenReturn(List.of(p1, p2));
        // heartbeat 분기 batch 조회 — 빈 결과
        when(roomRepo.findAllById(ArgumentMatchers.anyIterable())).thenReturn(List.of(fakeRoom(201L, false)));
        new RoomReconciliationScheduler(mgr, repo, roomRepo, props, fixedClock).scan();
        verify(mgr, times(1)).markProvisioningTimedOut(eq(201L), anyString());
        verify(mgr, never()).markProvisioningTimedOut(eq(202L), anyString());
    }

    @Test
    void scan_exceptionOnOneRow_doesNotStopOtherRows() {
        RoomServerInstanceRepository repo = mock(RoomServerInstanceRepository.class);
        RoomRepository roomRepo = mock(RoomRepository.class);
        RoomServerManager mgr = mock(RoomServerManager.class);
        Instant now = Instant.parse("2026-05-17T00:00:00Z");
        Clock fixedClock = Clock.fixed(now, ZoneOffset.UTC);
        RoomReconciliationProperties props = new RoomReconciliationProperties(
                Duration.ofSeconds(90), Duration.ofMinutes(5), Duration.ofSeconds(30));
        RoomServerInstance a = fakeInstance(301L, RoomServerInstanceStatus.READY, now.minusSeconds(200), now.minusSeconds(200));
        RoomServerInstance b = fakeInstance(302L, RoomServerInstanceStatus.READY, now.minusSeconds(200), now.minusSeconds(200));
        when(repo.findAllByStatusInAndLastHeartbeatAtBefore(ArgumentMatchers.anyList(), ArgumentMatchers.any())).thenReturn(List.of(a, b));
        when(repo.findAllByStatusIn(ArgumentMatchers.anyList())).thenReturn(List.of());
        Room normalA = fakeRoom(301L, false);
        Room normalB = fakeRoom(302L, false);
        when(roomRepo.findAllById(ArgumentMatchers.anyIterable())).thenReturn(List.of(normalA, normalB));
        doThrow(new RuntimeException("simulated")).when(mgr).markUnhealthyAndTerminate(eq(301L), anyString());
        RoomReconciliationScheduler sched = new RoomReconciliationScheduler(mgr, repo, roomRepo, props, fixedClock);
        Assertions.assertThatCode(sched::scan).doesNotThrowAnyException();
        verify(mgr, times(1)).markUnhealthyAndTerminate(eq(302L), anyString());
    }

    @Test
    void scan_heartbeatExpired_filtersOutPersistentRooms() {
        RoomServerInstanceRepository repo = mock(RoomServerInstanceRepository.class);
        RoomRepository roomRepo = mock(RoomRepository.class);
        RoomServerManager mgr = mock(RoomServerManager.class);
        Instant now = Instant.parse("2026-05-27T00:00:00Z");
        Clock fixedClock = Clock.fixed(now, ZoneOffset.UTC);
        RoomReconciliationProperties props = new RoomReconciliationProperties(
                Duration.ofSeconds(90), Duration.ofMinutes(5), Duration.ofSeconds(30));
        Instant threshold = now.minus(Duration.ofSeconds(90));

        // persistent 룸 1개 + 일반 룸 1개, 둘 다 heartbeat 임계 초과
        RoomServerInstance persistent = fakeInstance(401L, RoomServerInstanceStatus.READY, now.minusSeconds(200), now.minusSeconds(200));
        RoomServerInstance normal = fakeInstance(402L, RoomServerInstanceStatus.ACTIVE, now.minusSeconds(200), now.minusSeconds(200));
        when(repo.findAllByStatusInAndLastHeartbeatAtBefore(
                ArgumentMatchers.argThat(l -> l.contains(RoomServerInstanceStatus.READY)), eq(threshold)))
                .thenReturn(List.of(persistent, normal));
        when(repo.findAllByStatusIn(ArgumentMatchers.argThat(l -> l.contains(RoomServerInstanceStatus.PROVISIONING))))
                .thenReturn(List.of());

        Room persistentRoom = fakeRoom(401L, true);
        Room normalRoom = fakeRoom(402L, false);
        when(roomRepo.findAllById(ArgumentMatchers.anyIterable())).thenReturn(List.of(persistentRoom, normalRoom));

        new RoomReconciliationScheduler(mgr, repo, roomRepo, props, fixedClock).scan();

        // persistent 룸은 필터되고 일반 룸만 markUnhealthyAndTerminate 호출
        verify(mgr, never()).markUnhealthyAndTerminate(eq(401L), anyString());
        verify(mgr, times(1)).markUnhealthyAndTerminate(eq(402L), anyString());
    }

    @Test
    void scan_provisioningExpired_filtersOutPersistentRooms() {
        RoomServerInstanceRepository repo = mock(RoomServerInstanceRepository.class);
        RoomRepository roomRepo = mock(RoomRepository.class);
        RoomServerManager mgr = mock(RoomServerManager.class);
        Instant now = Instant.parse("2026-05-27T00:00:00Z");
        Clock fixedClock = Clock.fixed(now, ZoneOffset.UTC);
        RoomReconciliationProperties props = new RoomReconciliationProperties(
                Duration.ofSeconds(90), Duration.ofMinutes(5), Duration.ofSeconds(30));

        // persistent 룸 1개 + 일반 룸 1개, 둘 다 provisioning 임계 초과
        RoomServerInstance persistent = fakeInstance(501L, RoomServerInstanceStatus.PROVISIONING, now.minusSeconds(400), null);
        RoomServerInstance normal = fakeInstance(502L, RoomServerInstanceStatus.PROVISIONING, now.minusSeconds(400), null);
        when(repo.findAllByStatusInAndLastHeartbeatAtBefore(ArgumentMatchers.anyList(), ArgumentMatchers.any())).thenReturn(List.of());
        when(repo.findAllByStatusIn(ArgumentMatchers.argThat(l -> l.contains(RoomServerInstanceStatus.PROVISIONING))))
                .thenReturn(List.of(persistent, normal));

        Room persistentRoom = fakeRoom(501L, true);
        Room normalRoom = fakeRoom(502L, false);
        when(roomRepo.findAllById(ArgumentMatchers.anyIterable())).thenReturn(List.of(persistentRoom, normalRoom));

        new RoomReconciliationScheduler(mgr, repo, roomRepo, props, fixedClock).scan();

        // persistent 룸은 필터되고 일반 룸만 markProvisioningTimedOut 호출
        verify(mgr, never()).markProvisioningTimedOut(eq(501L), anyString());
        verify(mgr, times(1)).markProvisioningTimedOut(eq(502L), anyString());
    }

    private static RoomServerInstance fakeInstance(Long roomId, RoomServerInstanceStatus status,
            Instant createdAt, Instant lastHeartbeatAt) {
        RoomServerInstance instance = RoomServerInstance.provision(roomId, createdAt);
        switch (status) {
            case SERVER_STARTING: instance.attachEcsTask("c", "t"); break;
            case READY: instance.attachEcsTask("c", "t"); instance.markReady("0.0.0.0", 7777, createdAt);
                if (lastHeartbeatAt != null) instance.recordHeartbeat(lastHeartbeatAt); break;
            case ACTIVE: instance.attachEcsTask("c", "t"); instance.markReady("0.0.0.0", 7777, createdAt);
                if (lastHeartbeatAt != null) instance.recordHeartbeat(lastHeartbeatAt); instance.markActive(); break;
            default: break;
        }
        return instance;
    }

    private static Room fakeRoom(Long roomId, boolean isPersistent) {
        Room room = isPersistent
                ? Room.openPersistent(1L, "session-" + roomId, 8, null, Instant.now())
                : Room.open(1L, "session-" + roomId, 8, null, Instant.now());
        // @GeneratedValue 필드는 도메인 팩토리가 채우지 않으므로 테스트 시 reflection 으로 주입.
        ReflectionTestUtils.setField(room, "roomId", roomId);
        return room;
    }
}
