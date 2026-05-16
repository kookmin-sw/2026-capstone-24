package com.murang.room.domain;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;

@Entity
@Table(name = "room_server_instances")
public class RoomServerInstance {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "id", nullable = false)
    private Long id;

    @Column(name = "room_id", nullable = false, updatable = false)
    private Long roomId;

    @Enumerated(EnumType.STRING)
    @Column(name = "status", nullable = false, length = 32)
    private RoomServerInstanceStatus status;

    @Column(name = "ecs_cluster_arn", length = 512)
    private String ecsClusterArn;

    @Column(name = "ecs_task_arn", length = 512)
    private String ecsTaskArn;

    @Column(name = "task_public_ip", length = 45)
    private String taskPublicIp;

    @Column(name = "game_port")
    private Integer gamePort;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Column(name = "ready_at")
    private Instant readyAt;

    @Column(name = "last_heartbeat_at")
    private Instant lastHeartbeatAt;

    @Column(name = "terminated_at")
    private Instant terminatedAt;

    protected RoomServerInstance() {
    }

    public static RoomServerInstance provision(Long roomId, Instant now) {
        RoomServerInstance instance = new RoomServerInstance();
        instance.roomId = roomId;
        instance.status = RoomServerInstanceStatus.PROVISIONING;
        instance.createdAt = now;
        return instance;
    }

    public void attachEcsTask(String clusterArn, String taskArn) {
        this.ecsClusterArn = clusterArn;
        this.ecsTaskArn = taskArn;
        this.status = RoomServerInstanceStatus.SERVER_STARTING;
    }

    public void markReady(String publicIp, Integer gamePort, Instant now) {
        this.taskPublicIp = publicIp;
        this.gamePort = gamePort;
        this.status = RoomServerInstanceStatus.READY;
        this.readyAt = now;
        this.lastHeartbeatAt = now;
    }

    public void markActive() {
        if (status == RoomServerInstanceStatus.READY) {
            this.status = RoomServerInstanceStatus.ACTIVE;
        }
    }

    public void recordHeartbeat(Instant now) {
        this.lastHeartbeatAt = now;
    }

    public void markUnhealthy() {
        if (status == RoomServerInstanceStatus.READY || status == RoomServerInstanceStatus.ACTIVE) {
            this.status = RoomServerInstanceStatus.UNHEALTHY;
        }
    }

    public void beginTermination() {
        if (status != RoomServerInstanceStatus.TERMINATED) {
            this.status = RoomServerInstanceStatus.TERMINATING;
        }
    }

    public void markTerminated(Instant now) {
        this.status = RoomServerInstanceStatus.TERMINATED;
        this.terminatedAt = now;
    }

    public void markFailed(Instant now) {
        this.status = RoomServerInstanceStatus.FAILED;
        this.terminatedAt = now;
    }

    public Long getId() {
        return id;
    }

    public Long getRoomId() {
        return roomId;
    }

    public RoomServerInstanceStatus getStatus() {
        return status;
    }

    public String getEcsClusterArn() {
        return ecsClusterArn;
    }

    public String getEcsTaskArn() {
        return ecsTaskArn;
    }

    public String getTaskPublicIp() {
        return taskPublicIp;
    }

    public Integer getGamePort() {
        return gamePort;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public Instant getReadyAt() {
        return readyAt;
    }

    public Instant getLastHeartbeatAt() {
        return lastHeartbeatAt;
    }

    public Instant getTerminatedAt() {
        return terminatedAt;
    }
}
