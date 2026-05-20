package com.murang.room.controller.dto;

import jakarta.validation.constraints.NotBlank;

public record RoomTerminateCallbackRequest(
        @NotBlank String reason
) {
}
