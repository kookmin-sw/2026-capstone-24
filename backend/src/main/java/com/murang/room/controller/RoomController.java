package com.murang.room.controller;

import com.murang.auth.security.AuthPrincipal;
import com.murang.common.exception.ApiException;
import com.murang.common.response.ApiResponse;
import com.murang.room.controller.dto.RoomCreateRequest;
import com.murang.room.controller.dto.RoomCreateResponse;
import com.murang.room.manager.RoomProvisioningCommand;
import com.murang.room.manager.RoomServerManager;
import com.murang.room.manager.RoomServerSnapshot;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/v1/rooms")
public class RoomController {

    private final RoomServerManager roomServerManager;

    public RoomController(RoomServerManager roomServerManager) {
        this.roomServerManager = roomServerManager;
    }

    @PostMapping
    public ResponseEntity<ApiResponse<RoomCreateResponse>> create(
            @Valid @RequestBody RoomCreateRequest request
    ) {
        AuthPrincipal principal = currentPrincipal();

        RoomServerSnapshot snapshot = roomServerManager.provision(new RoomProvisioningCommand(
                principal.userId(),
                request.photonSessionName(),
                request.maxPlayers(),
                request.passwordHash(),
                request.roomRuntimeVersion()
        ));

        return ResponseEntity.ok(ApiResponse.ok(RoomCreateResponse.of(snapshot)));
    }

    private AuthPrincipal currentPrincipal() {
        Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
        if (authentication == null || !(authentication.getPrincipal() instanceof AuthPrincipal principal)) {
            throw ApiException.forbidden();
        }
        return principal;
    }
}
