package com.murang.auth.dto;

public record MetaLoginResponse(
        String accessToken,
        String refreshToken,
        UserSummary user
) {

    public record UserSummary(
            long userId,
            String nickname
    ) {
    }
}
