package com.murang.room.controller.dto;

import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

/**
 * Client → Spring 룸 생성 요청 body.
 *
 * <p>{@code photonSessionName} 은 Photon Cloud 의 매칭 키이자 본 backend 의
 * {@code rooms.photon_session_name} unique 컬럼 값으로 그대로 사용된다.
 *
 * <p>{@code passwordHash} 는 클라이언트가 {@code RoomPasswordHasher.Hash(raw)}
 * 로 만든 SHA256-base64 문자열 (44자) 또는 null/빈 문자열 (비밀번호 없음).
 * 본 endpoint 는 hash 만 저장하고 검증은 join ticket 발급 시에 수행한다.
 */
public record RoomCreateRequest(
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
