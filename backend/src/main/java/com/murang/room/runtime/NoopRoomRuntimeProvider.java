package com.murang.room.runtime;

import java.util.Optional;
import java.util.UUID;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Default {@link RoomRuntimeProvider} used in local dev or test profiles where
 * Spring does not orchestrate dedicated server processes (they are launched by
 * docker-compose directly or omitted altogether). Returns synthetic ARNs so the
 * lifecycle service can still progress through state transitions without
 * touching real infrastructure.
 */
public class NoopRoomRuntimeProvider implements RoomRuntimeProvider {

    private static final Logger log = LoggerFactory.getLogger(NoopRoomRuntimeProvider.class);

    @Override
    public ProvisionedRoomTask startRoomTask(RoomTaskStartRequest request) {
        String taskArn = "noop:task:" + UUID.randomUUID();
        log.info("noop runtime provider: pretending to start room task room_id={} task_arn={}",
                request.roomId(), taskArn);
        return new ProvisionedRoomTask("noop:cluster", taskArn);
    }

    @Override
    public void stopRoomTask(RoomTaskStopRequest request) {
        log.info("noop runtime provider: pretending to stop room task room_id={} task_arn={} reason={}",
                request.roomId(), request.taskArn(), request.reason());
    }

    @Override
    public Optional<RoomTaskRuntimeState> describeRoomTask(String clusterArn, String taskArn) {
        return Optional.empty();
    }
}
