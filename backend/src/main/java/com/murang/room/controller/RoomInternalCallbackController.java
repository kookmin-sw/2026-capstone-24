package com.murang.room.controller;

import com.murang.common.exception.ApiException;
import com.murang.room.config.RoomInternalCallbackProperties;
import com.murang.room.controller.dto.RoomReadyCallbackRequest;
import com.murang.room.manager.RoomReadySignal;
import com.murang.room.manager.RoomServerManager;
import jakarta.validation.Valid;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.time.Clock;
import java.time.Instant;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/internal/rooms")
public class RoomInternalCallbackController {

    static final String INTERNAL_TOKEN_HEADER = "X-Internal-Token";

    private final RoomServerManager roomServerManager;
    private final RoomInternalCallbackProperties properties;
    private final Clock clock;

    public RoomInternalCallbackController(
            RoomServerManager roomServerManager,
            RoomInternalCallbackProperties properties,
            Clock clock
    ) {
        this.roomServerManager = roomServerManager;
        this.properties = properties;
        this.clock = clock;
    }

    @PostMapping("/{roomId}/ready")
    public ResponseEntity<Void> ready(
            @PathVariable Long roomId,
            @RequestHeader(value = INTERNAL_TOKEN_HEADER, required = false) String token,
            @Valid @RequestBody RoomReadyCallbackRequest request
    ) {
        verifyInternalToken(token);
        roomServerManager.notifyReady(roomId, new RoomReadySignal(
                request.taskPublicIp(),
                request.gamePort(),
                request.roomRuntimeVersion()
        ));
        return ResponseEntity.noContent().build();
    }

    @PostMapping("/{roomId}/heartbeat")
    public ResponseEntity<Void> heartbeat(
            @PathVariable Long roomId,
            @RequestHeader(value = INTERNAL_TOKEN_HEADER, required = false) String token
    ) {
        verifyInternalToken(token);
        roomServerManager.notifyHeartbeat(roomId, Instant.now(clock));
        return ResponseEntity.noContent().build();
    }

    private void verifyInternalToken(String token) {
        String expected = properties.sharedSecret();
        if (expected == null || expected.isBlank()) {
            return;
        }
        if (token == null || !constantTimeEquals(token, expected)) {
            throw ApiException.roomInternalForbidden();
        }
    }

    private static boolean constantTimeEquals(String a, String b) {
        byte[] ab = a.getBytes(StandardCharsets.UTF_8);
        byte[] bb = b.getBytes(StandardCharsets.UTF_8);
        return MessageDigest.isEqual(ab, bb);
    }
}
