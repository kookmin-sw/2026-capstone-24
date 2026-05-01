package com.murang.user.dto;

public record UserMeResponse(
        String playerId,
        String metaAccountId,
        String nickname
) {
}
