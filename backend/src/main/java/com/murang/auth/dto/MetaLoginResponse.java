package com.murang.auth.dto;

public record MetaLoginResponse(
        String accessToken,
        String refreshToken,
        UserSummary user
) {

    public record UserSummary(
            String playerId,
            String nickname
    ) {
    }
}
