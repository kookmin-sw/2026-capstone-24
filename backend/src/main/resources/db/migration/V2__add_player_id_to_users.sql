ALTER TABLE users
    ADD COLUMN player_id VARCHAR(36) NULL;

ALTER TABLE users
    ADD CONSTRAINT uk_users_player_id UNIQUE (player_id);
