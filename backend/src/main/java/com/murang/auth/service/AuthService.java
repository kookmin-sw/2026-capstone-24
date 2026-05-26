package com.murang.auth.service;

import com.murang.auth.dto.MetaLoginRequest;
import com.murang.auth.dto.MetaLoginResponse;
import com.murang.auth.dto.RefreshTokenRequest;
import com.murang.common.exception.ApiException;
import com.murang.user.domain.UserProfile;
import com.murang.user.service.UserRegistry;
import java.util.Optional;
import org.springframework.stereotype.Service;

@Service
public class AuthService {

    private final MetaIdTokenVerifier metaIdTokenVerifier;
    private final UserRegistry userRegistry;
    private final JwtTokenService jwtTokenService;

    public AuthService(
            MetaIdTokenVerifier metaIdTokenVerifier,
            UserRegistry userRegistry,
            JwtTokenService jwtTokenService
    ) {
        this.metaIdTokenVerifier = metaIdTokenVerifier;
        this.userRegistry = userRegistry;
        this.jwtTokenService = jwtTokenService;
    }

    public MetaLoginResponse login(MetaLoginRequest request) {
        MetaIdentity identity = metaIdTokenVerifier.verify(request.metaIdToken());

        Optional<UserProfile> existing = userRegistry.findByMetaAccountId(identity.metaAccountId());
        if (existing.isPresent()) {
            UserProfile user = existing.get();
            JwtTokenService.IssuedTokens tokens = jwtTokenService.issueTokens(user);
            return toMetaLoginResponse(user, tokens);
        }

        // 신규 유저
        String rawNickname = request.nickname();
        if (rawNickname == null || rawNickname.isBlank()) {
            throw ApiException.nicknameRequired();
        }
        String normalizedNickname = normalizeNickname(rawNickname);
        UserProfile created = userRegistry.register(identity.metaAccountId(), normalizedNickname);
        JwtTokenService.IssuedTokens tokens = jwtTokenService.issueTokens(created);
        return toMetaLoginResponse(created, tokens);
    }

    public MetaLoginResponse refresh(RefreshTokenRequest request) {
        JwtTokenService.RefreshTokenIdentity refreshIdentity = jwtTokenService.parseRefreshToken(request.refreshToken());
        UserProfile user = findRefreshUser(refreshIdentity)
                .orElseThrow(ApiException::invalidJwt);
        JwtTokenService.IssuedTokens tokens = jwtTokenService.issueTokens(user);
        return toMetaLoginResponse(user, tokens);
    }

    private String normalizeNickname(String rawNickname) {
        return rawNickname.trim();
    }

    private MetaLoginResponse toMetaLoginResponse(UserProfile user, JwtTokenService.IssuedTokens tokens) {
        return new MetaLoginResponse(
                tokens.accessToken(),
                tokens.refreshToken(),
                new MetaLoginResponse.UserSummary(user.playerId(), user.nickname())
        );
    }

    private java.util.Optional<UserProfile> findRefreshUser(JwtTokenService.RefreshTokenIdentity refreshIdentity) {
        if (refreshIdentity.playerId() != null && !refreshIdentity.playerId().isBlank()) {
            return userRegistry.findByPlayerId(refreshIdentity.playerId())
                    .or(() -> userRegistry.findByMetaAccountId(refreshIdentity.metaAccountId()));
        }

        return userRegistry.findByMetaAccountId(refreshIdentity.metaAccountId());
    }
}
