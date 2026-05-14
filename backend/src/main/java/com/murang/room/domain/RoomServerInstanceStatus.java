package com.murang.room.domain;

public enum RoomServerInstanceStatus {
    PROVISIONING,
    SERVER_STARTING,
    READY,
    ACTIVE,
    UNHEALTHY,
    TERMINATING,
    TERMINATED,
    FAILED
}
