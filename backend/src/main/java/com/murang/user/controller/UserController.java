package com.murang.user.controller;

import com.murang.auth.security.AuthPrincipal;
import com.murang.common.exception.ApiException;
import com.murang.common.response.ApiResponse;
import com.murang.user.dto.UserMeResponse;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/v1/users")
public class UserController {

    @GetMapping("/me")
    public ResponseEntity<ApiResponse<UserMeResponse>> me() {
        Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
        if (authentication == null || !(authentication.getPrincipal() instanceof AuthPrincipal principal)) {
            throw ApiException.forbidden();
        }

        return ResponseEntity.ok(ApiResponse.ok(new UserMeResponse(
                principal.userId(),
                principal.metaAccountId(),
                principal.nickname()
        )));
    }
}
