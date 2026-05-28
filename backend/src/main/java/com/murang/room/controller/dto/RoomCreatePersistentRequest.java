package com.murang.room.controller.dto;

import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.Size;

/**
 * 운영자 전용 persistent 룸 생성 요청 body.
 * {@code POST /internal/rooms/persistent} — {@code X-Internal-Token} 인증 필수.
 */
public record RoomCreatePersistentRequest(
        @Positive
        Long ownerUserId,

        @NotBlank
        @Size(max = 128)
        @Pattern(regexp = "^[A-Za-z0-9_\\-]+$",
                message = "룸 세션 이름은 영문/숫자/_/- 만 허용됩니다.")
        String photonSessionName,

        @Min(1) @Max(32)
        int maxPlayers,

        @Size(max = 64)
        String passwordHash,

        @NotBlank
        @Size(max = 64)
        String roomRuntimeVersion
) {
}
