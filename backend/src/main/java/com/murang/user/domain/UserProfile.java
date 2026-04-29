package com.murang.user.domain;

import java.time.Instant;

public record UserProfile(
        long userId,
        String metaAccountId,
        String nickname,
        Instant createdAt,
        Instant lastLoginAt
) {
}
