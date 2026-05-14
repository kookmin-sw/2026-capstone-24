CREATE TABLE rooms (
    room_id BIGINT NOT NULL AUTO_INCREMENT,
    owner_user_id BIGINT NOT NULL,
    photon_session_name VARCHAR(128) NOT NULL,
    max_players INT NOT NULL,
    created_at TIMESTAMP(6) NOT NULL,
    closed_at TIMESTAMP(6) NULL,
    CONSTRAINT pk_rooms PRIMARY KEY (room_id),
    CONSTRAINT uk_rooms_photon_session_name UNIQUE (photon_session_name),
    CONSTRAINT fk_rooms_owner_user_id FOREIGN KEY (owner_user_id) REFERENCES users (user_id)
);

CREATE INDEX idx_rooms_owner_user_id ON rooms (owner_user_id);
CREATE INDEX idx_rooms_closed_at ON rooms (closed_at);
