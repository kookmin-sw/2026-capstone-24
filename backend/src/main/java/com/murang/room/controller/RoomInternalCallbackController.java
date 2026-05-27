package com.murang.room.controller;

import com.murang.common.response.ApiResponse;
import com.murang.common.exception.ApiException;
import com.murang.room.config.RoomInternalCallbackProperties;
import com.murang.room.controller.dto.RoomCreatePersistentRequest;
import com.murang.room.controller.dto.RoomReadyCallbackRequest;
import com.murang.room.controller.dto.RoomResponse;
import com.murang.room.controller.dto.RoomTerminateCallbackRequest;
import com.murang.room.domain.RoomServerInstanceStatus;
import com.murang.room.manager.RoomProvisioningCommand;
import com.murang.room.manager.RoomReadySignal;
import com.murang.room.manager.RoomServerManager;
import com.murang.room.manager.RoomServerSnapshot;
import jakarta.validation.Valid;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.time.Clock;
import java.time.Instant;
import java.util.Optional;
import java.util.Set;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
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

    private static final Logger log = LoggerFactory.getLogger(RoomInternalCallbackController.class);

    static final String INTERNAL_TOKEN_HEADER = "X-Internal-Token";

    private static final Set<RoomServerInstanceStatus> TERMINAL_STATUSES = Set.of(
            RoomServerInstanceStatus.TERMINATED, RoomServerInstanceStatus.FAILED);

    private final RoomServerManager roomServerManager;
    private final RoomInternalCallbackProperties properties;
    private final Clock clock;

    public RoomInternalCallbackController(RoomServerManager roomServerManager,
            RoomInternalCallbackProperties properties, Clock clock) {
        this.roomServerManager = roomServerManager;
        this.properties = properties;
        this.clock = clock;
    }

    @PostMapping("/{roomId}/ready")
    public ResponseEntity<Void> ready(
            @PathVariable Long roomId,
            @RequestHeader(value = INTERNAL_TOKEN_HEADER, required = false) String token,
            @Valid @RequestBody RoomReadyCallbackRequest request) {
        verifyInternalToken(token);
        roomServerManager.notifyReady(roomId, new RoomReadySignal(
                request.taskPublicIp(), request.gamePort(), request.roomRuntimeVersion()));
        return ResponseEntity.noContent().build();
    }

    @PostMapping("/{roomId}/heartbeat")
    public ResponseEntity<Void> heartbeat(
            @PathVariable Long roomId,
            @RequestHeader(value = INTERNAL_TOKEN_HEADER, required = false) String token) {
        verifyInternalToken(token);
        roomServerManager.notifyHeartbeat(roomId, Instant.now(clock));
        return ResponseEntity.noContent().build();
    }

    @PostMapping("/{roomId}/terminate")
    public ResponseEntity<Void> terminate(
            @PathVariable Long roomId,
            @RequestHeader(value = INTERNAL_TOKEN_HEADER, required = false) String token,
            @Valid @RequestBody RoomTerminateCallbackRequest request) {
        verifyInternalToken(token);
        Optional<RoomServerSnapshot> snapshot = roomServerManager.findByRoomId(roomId);
        if (snapshot.isEmpty() || TERMINAL_STATUSES.contains(snapshot.get().status())) {
            return ResponseEntity.noContent().build();
        }
        // persistent 룸은 terminate 콜백을 idempotent skip — DS 측 분기가 누락되더라도 이중 안전망
        if (snapshot.get().isPersistent()) {
            log.info("terminate skipped (persistent room) room_id={}", roomId);
            return ResponseEntity.noContent().build();
        }
        roomServerManager.terminate(roomId, "ds-callback:" + request.reason());
        return ResponseEntity.noContent().build();
    }

    @PostMapping("/persistent")
    public ResponseEntity<ApiResponse<RoomResponse>> createPersistent(
            @RequestHeader(value = INTERNAL_TOKEN_HEADER, required = false) String token,
            @Valid @RequestBody RoomCreatePersistentRequest request) {
        verifyInternalToken(token);
        RoomServerSnapshot snapshot = roomServerManager.provisionPersistent(
                new RoomProvisioningCommand(
                        request.ownerUserId(),
                        request.photonSessionName(),
                        request.maxPlayers(),
                        request.passwordHash(),
                        request.roomRuntimeVersion()));
        return ResponseEntity.ok(ApiResponse.ok(RoomResponse.of(snapshot)));
    }

    private void verifyInternalToken(String token) {
        String expected = properties.sharedSecret();
        if (expected == null || expected.isBlank()) { return; }
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
