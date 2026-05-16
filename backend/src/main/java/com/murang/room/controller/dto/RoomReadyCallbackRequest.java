package com.murang.room.controller.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Positive;

/**
 * Dedicated server → Spring ready 콜백 body.
 *
 * <p>{@code gamePort} 는 Photon Cloud relay 모델에서는 필요 없으므로 nullable.
 * 직접 UDP 합류 모델로 전환할 경우 dedicated server 가 자신의 listen port 를
 * 넣어 보고하도록 한다.
 */
public record RoomReadyCallbackRequest(
        @NotBlank String taskPublicIp,
        @Positive Integer gamePort,
        @NotBlank String roomRuntimeVersion
) {
}
