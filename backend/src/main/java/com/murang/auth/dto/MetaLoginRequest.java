package com.murang.auth.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

public record MetaLoginRequest(
        @NotBlank(message = "Meta ID 토큰은 필수입니다.")
        @Size(min = 12, max = 4096, message = "Meta ID 토큰 길이가 허용 범위를 벗어났습니다.")
        String metaIdToken,

        @NotBlank(message = "닉네임은 필수입니다.")
        @Size(min = 2, max = 32, message = "닉네임은 2자 이상 32자 이하여야 합니다.")
        @Pattern(
                regexp = "^[\\p{L}\\p{N} ]{2,32}$",
                message = "닉네임은 한글, 영문, 숫자, 공백만 사용할 수 있습니다."
        )
        String nickname
) {
}
