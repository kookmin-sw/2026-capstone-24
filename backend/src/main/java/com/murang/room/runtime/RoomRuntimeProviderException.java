package com.murang.room.runtime;

import java.io.Serial;

/**
 * Thrown by {@link RoomRuntimeProvider} implementations when the underlying
 * runtime (ECS Fargate, Docker, etc.) fails to start, stop, or describe a room
 * task. {@code RoomServerManagerImpl.provision()} catches this and converts it
 * into {@code ApiException.roomProvisioningFailed()}.
 */
public class RoomRuntimeProviderException extends RuntimeException {

    @Serial
    private static final long serialVersionUID = 1L;

    public RoomRuntimeProviderException(String message) {
        super(message);
    }

    public RoomRuntimeProviderException(String message, Throwable cause) {
        super(message, cause);
    }
}
