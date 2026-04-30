package com.murang.auth.service;

import com.murang.auth.dto.MetaLoginRequest;
import com.murang.auth.dto.MetaLoginResponse;
import com.murang.auth.dto.RefreshTokenRequest;
import com.murang.common.exception.ApiException;
import com.murang.user.domain.UserProfile;
import com.murang.user.service.UserRegistry;
import java.util.regex.Pattern;
import org.springframework.stereotype.Service;

@Service
public class AuthService {

    private static final Pattern MULTI_SPACE = Pattern.compile("\\s+");

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
        String normalizedNickname = normalizeNickname(request.nickname());

        UserProfile user = userRegistry.registerOrUpdate(identity.metaAccountId(), normalizedNickname);
        JwtTokenService.IssuedTokens tokens = jwtTokenService.issueTokens(user);

        return toMetaLoginResponse(user, tokens);
    }

    public MetaLoginResponse refresh(RefreshTokenRequest request) {
        String metaAccountId = jwtTokenService.parseRefreshToken(request.refreshToken());
        UserProfile user = userRegistry.findByMetaAccountId(metaAccountId)
                .orElseThrow(ApiException::invalidJwt);
        JwtTokenService.IssuedTokens tokens = jwtTokenService.issueTokens(user);
        return toMetaLoginResponse(user, tokens);
    }

    private String normalizeNickname(String rawNickname) {
        String trimmed = MULTI_SPACE.matcher(rawNickname.trim()).replaceAll(" ");
        if (trimmed.isBlank()) {
            throw ApiException.invalidNickname("닉네임은 공백만으로 구성될 수 없습니다.");
        }
        return trimmed;
    }

    private MetaLoginResponse toMetaLoginResponse(UserProfile user, JwtTokenService.IssuedTokens tokens) {
        return new MetaLoginResponse(
                tokens.accessToken(),
                tokens.refreshToken(),
                new MetaLoginResponse.UserSummary(user.userId(), user.nickname())
        );
    }
}
