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
import com.murang.room.domain.RoomServerInstance;
import com.murang.room.domain.RoomServerInstanceStatus;
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
        new RoomReconciliationScheduler(mgr, repo, props, fixedClock).scan();
        verify(mgr, times(1)).markUnhealthyAndTerminate(eq(101L), anyString());
        verify(mgr, times(1)).markUnhealthyAndTerminate(eq(102L), anyString());
    }

    @Test
    void scan_provisioningExpired_callsMarkProvisioningTimedOutForExpiredOnly() {
        RoomServerInstanceRepository repo = mock(RoomServerInstanceRepository.class);
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
        new RoomReconciliationScheduler(mgr, repo, props, fixedClock).scan();
        verify(mgr, times(1)).markProvisioningTimedOut(eq(201L), anyString());
        verify(mgr, never()).markProvisioningTimedOut(eq(202L), anyString());
    }

    @Test
    void scan_exceptionOnOneRow_doesNotStopOtherRows() {
        RoomServerInstanceRepository repo = mock(RoomServerInstanceRepository.class);
        RoomServerManager mgr = mock(RoomServerManager.class);
        Instant now = Instant.parse("2026-05-17T00:00:00Z");
        Clock fixedClock = Clock.fixed(now, ZoneOffset.UTC);
        RoomReconciliationProperties props = new RoomReconciliationProperties(
                Duration.ofSeconds(90), Duration.ofMinutes(5), Duration.ofSeconds(30));
        RoomServerInstance a = fakeInstance(301L, RoomServerInstanceStatus.READY, now.minusSeconds(200), now.minusSeconds(200));
        RoomServerInstance b = fakeInstance(302L, RoomServerInstanceStatus.READY, now.minusSeconds(200), now.minusSeconds(200));
        when(repo.findAllByStatusInAndLastHeartbeatAtBefore(ArgumentMatchers.anyList(), ArgumentMatchers.any())).thenReturn(List.of(a, b));
        when(repo.findAllByStatusIn(ArgumentMatchers.anyList())).thenReturn(List.of());
        doThrow(new RuntimeException("simulated")).when(mgr).markUnhealthyAndTerminate(eq(301L), anyString());
        RoomReconciliationScheduler sched = new RoomReconciliationScheduler(mgr, repo, props, fixedClock);
        Assertions.assertThatCode(sched::scan).doesNotThrowAnyException();
        verify(mgr, times(1)).markUnhealthyAndTerminate(eq(302L), anyString());
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
}
