package com.murang.room.controller.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;

public record RoomReadyCallbackRequest(
        @NotBlank String taskPublicIp,
        @NotNull @Positive Integer gamePort,
        @NotBlank String roomRuntimeVersion
) {
}
