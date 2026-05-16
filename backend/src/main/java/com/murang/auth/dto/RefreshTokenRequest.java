package com.murang.auth.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record RefreshTokenRequest(
        @NotBlank(message = "Refresh Token은 필수입니다.")
        @Size(max = 4096, message = "Refresh Token 길이가 허용 범위를 벗어났습니다.")
        String refreshToken
) {
}
