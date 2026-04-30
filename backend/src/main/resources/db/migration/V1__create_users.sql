CREATE TABLE users (
    user_id BIGINT NOT NULL AUTO_INCREMENT,
    meta_account_id VARCHAR(128) NOT NULL,
    nickname VARCHAR(32) NOT NULL,
    nickname_key VARCHAR(128) NOT NULL,
    created_at TIMESTAMP(6) NOT NULL,
    last_login_at TIMESTAMP(6) NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (user_id),
    CONSTRAINT uk_users_meta_account_id UNIQUE (meta_account_id),
    CONSTRAINT uk_users_nickname_key UNIQUE (nickname_key)
);
