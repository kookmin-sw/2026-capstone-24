package com.murang.room.observability;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.murang.room.domain.RoomServerInstanceStatus;
import com.murang.room.manager.RoomServerManager;
import com.murang.room.manager.RoomServerSnapshot;
import io.micrometer.core.instrument.Gauge;
import io.micrometer.core.instrument.simple.SimpleMeterRegistry;
import java.time.Instant;
import java.util.List;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

class ActiveRoomCountMetricBinderTest {

    private SimpleMeterRegistry registry;
    private RoomServerManager manager;

    @BeforeEach
    void setUp() {
        registry = new SimpleMeterRegistry();
        manager = mock(RoomServerManager.class);
    }

    @Test
    void registersGaugeNamedActiveRoomCount() {
        when(manager.findAdmissionOpen()).thenReturn(List.of());

        new ActiveRoomCountMetricBinder(registry, manager);

        Gauge gauge = registry.find(ActiveRoomCountMetricBinder.METRIC_NAME).gauge();
        assertThat(gauge).isNotNull();
        assertThat(gauge.getId().getDescription()).contains("admission");
    }

    @Test
    void gaugeReportsZeroWhenNoAdmissionOpenInstances() {
        when(manager.findAdmissionOpen()).thenReturn(List.of());

        new ActiveRoomCountMetricBinder(registry, manager);

        double value = registry.get(ActiveRoomCountMetricBinder.METRIC_NAME).gauge().value();
        assertThat(value).isEqualTo(0.0);
    }

    @Test
    void gaugeReflectsCountReportedByRoomServerManager() {
        when(manager.findAdmissionOpen()).thenReturn(List.of(
                snapshot(RoomServerInstanceStatus.READY),
                snapshot(RoomServerInstanceStatus.ACTIVE),
                snapshot(RoomServerInstanceStatus.READY)
        ));

        new ActiveRoomCountMetricBinder(registry, manager);

        double value = registry.get(ActiveRoomCountMetricBinder.METRIC_NAME).gauge().value();
        assertThat(value).isEqualTo(3.0);
    }

    @Test
    void gaugeRecomputesOnEachSampleSoSubsequentCallsReflectStateChange() {
        when(manager.findAdmissionOpen()).thenReturn(List.of(snapshot(RoomServerInstanceStatus.READY)));
        new ActiveRoomCountMetricBinder(registry, manager);

        Gauge gauge = registry.get(ActiveRoomCountMetricBinder.METRIC_NAME).gauge();
        assertThat(gauge.value()).isEqualTo(1.0);

        when(manager.findAdmissionOpen()).thenReturn(List.of(
                snapshot(RoomServerInstanceStatus.READY),
                snapshot(RoomServerInstanceStatus.ACTIVE)
        ));
        assertThat(gauge.value()).isEqualTo(2.0);
    }

    private static RoomServerSnapshot snapshot(RoomServerInstanceStatus status) {
        return new RoomServerSnapshot(
                1L,
                1L,
                "session",
                4,
                false,
                10L,
                status,
                "cluster-arn",
                "task-arn",
                "1.2.3.4",
                7777,
                Instant.parse("2026-05-16T00:00:00Z"),
                Instant.parse("2026-05-16T00:00:05Z"),
                Instant.parse("2026-05-16T00:00:10Z"),
                null,
                null
        );
    }
}
