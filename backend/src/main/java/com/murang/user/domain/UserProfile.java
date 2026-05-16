package com.murang.user.domain;

import java.time.Instant;

public record UserProfile(
        long userId,
        String playerId,
        String metaAccountId,
        String nickname,
        Instant createdAt,
        Instant lastLoginAt
) {
}
