package com.murang.room.observability;

import com.murang.room.manager.RoomServerManager;
import io.micrometer.core.instrument.Gauge;
import io.micrometer.core.instrument.MeterRegistry;
import org.springframework.stereotype.Component;

/**
 * Registers the {@code active_room_count} gauge against the application
 * {@link MeterRegistry}. The gauge reports the current number of admission-open
 * room server instances ({@code READY} or {@code ACTIVE}) so the AWS dev
 * topology runbook can satisfy the "minimum 1 custom metric published"
 * acceptance criterion in the
 * {@code 2026-05-07-namae1128-aws-dev-topology-ec2-fargate} plan.
 */
@Component
public class ActiveRoomCountMetricBinder {

    static final String METRIC_NAME = "active_room_count";

    public ActiveRoomCountMetricBinder(MeterRegistry meterRegistry, RoomServerManager roomServerManager) {
        Gauge.builder(METRIC_NAME, roomServerManager, manager -> manager.findAdmissionOpen().size())
                .description("Number of READY or ACTIVE room server instances accepting admission")
                .strongReference(true)
                .register(meterRegistry);
    }
}
