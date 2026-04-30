package com.murang.user.dto;

public record UserMeResponse(
        long userId,
        String metaAccountId,
        String nickname
) {
}
