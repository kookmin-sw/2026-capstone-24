package com.murang.auth.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

public record MetaLoginRequest(
        @NotBlank(message = "Meta ID 토큰은 필수입니다.")
        @Size(min = 12, max = 4096, message = "Meta ID 토큰 길이가 허용 범위를 벗어났습니다.")
        String metaIdToken,

        @Size(max = 16, message = "닉네임은 16자 이하여야 합니다.")
        @Pattern(
                regexp = "^[A-Za-z0-9_-]{1,16}$|^$",
                message = "닉네임은 영문/숫자/_/- 만 1~16자."
        )
        String nickname
) {
}
