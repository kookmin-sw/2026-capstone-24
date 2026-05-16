package com.murang.room.runtime;

import java.util.Optional;

/**
 * Room instance runtime SPI. ECS Fargate, local Docker, or future Kubernetes
 * implementations plug in via this interface so {@code RoomServerManager} does
 * not depend on concrete infrastructure details.
 */
public interface RoomRuntimeProvider {

    ProvisionedRoomTask startRoomTask(RoomTaskStartRequest request);

    void stopRoomTask(RoomTaskStopRequest request);

    Optional<RoomTaskRuntimeState> describeRoomTask(String clusterArn, String taskArn);
}
