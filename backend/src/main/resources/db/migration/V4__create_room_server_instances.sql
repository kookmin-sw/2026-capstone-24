CREATE TABLE room_server_instances (
    id BIGINT NOT NULL AUTO_INCREMENT,
    room_id BIGINT NOT NULL,
    status VARCHAR(32) NOT NULL,
    ecs_cluster_arn VARCHAR(512) NULL,
    ecs_task_arn VARCHAR(512) NULL,
    task_public_ip VARCHAR(45) NULL,
    game_port INT NULL,
    created_at TIMESTAMP(6) NOT NULL,
    ready_at TIMESTAMP(6) NULL,
    last_heartbeat_at TIMESTAMP(6) NULL,
    terminated_at TIMESTAMP(6) NULL,
    CONSTRAINT pk_room_server_instances PRIMARY KEY (id),
    CONSTRAINT fk_room_server_instances_room_id FOREIGN KEY (room_id) REFERENCES rooms (room_id)
);

CREATE INDEX idx_room_server_instances_room_id ON room_server_instances (room_id);
CREATE INDEX idx_room_server_instances_status ON room_server_instances (status);
CREATE INDEX idx_room_server_instances_last_heartbeat_at ON room_server_instances (last_heartbeat_at);
