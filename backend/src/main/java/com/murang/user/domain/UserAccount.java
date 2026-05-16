package com.murang.user.domain;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import java.time.Instant;
import java.util.Locale;

@Entity
@Table(
        name = "users",
        uniqueConstraints = {
                @UniqueConstraint(name = "uk_users_player_id", columnNames = "player_id"),
                @UniqueConstraint(name = "uk_users_meta_account_id", columnNames = "meta_account_id"),
                @UniqueConstraint(name = "uk_users_nickname_key", columnNames = "nickname_key")
        }
)
public class UserAccount {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "user_id", nullable = false)
    private Long userId;

    @Column(name = "meta_account_id", nullable = false, length = 128, updatable = false)
    private String metaAccountId;

    @Column(name = "player_id", length = 26)
    private String playerId;

    @Column(name = "nickname", nullable = false, length = 32)
    private String nickname;

    @Column(name = "nickname_key", nullable = false, length = 128)
    private String nicknameKey;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Column(name = "last_login_at", nullable = false)
    private Instant lastLoginAt;

    protected UserAccount() {
    }

    public static UserAccount create(String metaAccountId, String playerId, String nickname, Instant now) {
        UserAccount userAccount = new UserAccount();
        userAccount.metaAccountId = metaAccountId;
        userAccount.playerId = playerId;
        userAccount.nickname = nickname;
        userAccount.nicknameKey = nicknameKeyOf(nickname);
        userAccount.createdAt = now;
        userAccount.lastLoginAt = now;
        return userAccount;
    }

    public boolean assignPlayerIdIfNeeded(String playerId) {
        if (PlayerIdGenerator.isUlid(this.playerId)) {
            return false;
        }

        this.playerId = playerId;
        return true;
    }

    public void recordLogin(String nickname, Instant now) {
        this.nickname = nickname;
        this.nicknameKey = nicknameKeyOf(nickname);
        this.lastLoginAt = now;
    }

    public UserProfile toProfile() {
        return new UserProfile(userId, playerId, metaAccountId, nickname, createdAt, lastLoginAt);
    }

    public Long getUserId() {
        return userId;
    }

    public String getMetaAccountId() {
        return metaAccountId;
    }

    public String getPlayerId() {
        return playerId;
    }

    public String getNicknameKey() {
        return nicknameKey;
    }

    public static String nicknameKeyOf(String nickname) {
        return nickname.strip().toLowerCase(Locale.ROOT);
    }
}
