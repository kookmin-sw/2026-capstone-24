package com.murang.auth.service;

import com.murang.auth.config.SecurityProperties;
import com.murang.auth.security.AuthPrincipal;
import com.murang.common.exception.ApiException;
import com.murang.user.domain.UserProfile;
import io.jsonwebtoken.Claims;
import io.jsonwebtoken.Jws;
import io.jsonwebtoken.JwtException;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.io.Decoders;
import io.jsonwebtoken.security.Keys;
import java.nio.charset.StandardCharsets;
import java.security.SecureRandom;
import java.time.Instant;
import java.util.Date;
import java.util.UUID;
import javax.crypto.SecretKey;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.util.StringUtils;

@Service
public class JwtTokenService {

    private static final Logger log = LoggerFactory.getLogger(JwtTokenService.class);
    private static final int HS512_MINIMUM_KEY_BYTES = 64;
    private static final String CLAIM_USER_ID = "userId";
    private static final String CLAIM_NICKNAME = "nickname";
    private static final String CLAIM_TOKEN_TYPE = "tokenType";
    private static final String TOKEN_TYPE_ACCESS = "access";
    private static final String TOKEN_TYPE_REFRESH = "refresh";

    private final SecurityProperties securityProperties;
    private final SecretKey secretKey;
    private final SecureRandom secureRandom = new SecureRandom();

    public JwtTokenService(SecurityProperties securityProperties) {
        this.securityProperties = securityProperties;
        this.secretKey = resolveSecretKey(securityProperties);
    }

    public IssuedTokens issueTokens(UserProfile userProfile) {
        Instant now = Instant.now();
        String accessToken = buildToken(userProfile, TOKEN_TYPE_ACCESS, now, now.plus(securityProperties.getJwt().getAccessTokenTtl()));
        String refreshToken = buildToken(userProfile, TOKEN_TYPE_REFRESH, now, now.plus(securityProperties.getJwt().getRefreshTokenTtl()));
        return new IssuedTokens(accessToken, refreshToken);
    }

    public AuthPrincipal parseAccessToken(String token) {
        Claims claims = parseAndValidate(token, TOKEN_TYPE_ACCESS);

        Object userIdClaim = claims.get(CLAIM_USER_ID);
        if (!(userIdClaim instanceof Number number)) {
            throw ApiException.invalidJwt();
        }

        String metaAccountId = claims.getSubject();
        String nickname = claims.get(CLAIM_NICKNAME, String.class);
        if (!StringUtils.hasText(metaAccountId) || !StringUtils.hasText(nickname)) {
            throw ApiException.invalidJwt();
        }

        return new AuthPrincipal(number.longValue(), metaAccountId, nickname);
    }

    public String parseRefreshToken(String token) {
        Claims claims = parseAndValidate(token, TOKEN_TYPE_REFRESH);
        String metaAccountId = claims.getSubject();
        if (!StringUtils.hasText(metaAccountId)) {
            throw ApiException.invalidJwt();
        }
        return metaAccountId;
    }

    private Claims parseAndValidate(String token, String expectedTokenType) {
        try {
            Jws<Claims> jws = Jwts.parser()
                    .verifyWith(secretKey)
                    .build()
                    .parseSignedClaims(token);
            Claims claims = jws.getPayload();

            String issuer = claims.getIssuer();
            String tokenType = claims.get(CLAIM_TOKEN_TYPE, String.class);
            if (!securityProperties.getJwt().getIssuer().equals(issuer) || !expectedTokenType.equals(tokenType)) {
                throw ApiException.invalidJwt();
            }

            return claims;
        } catch (JwtException | IllegalArgumentException exception) {
            throw ApiException.invalidJwt();
        }
    }

    private String buildToken(UserProfile userProfile, String tokenType, Instant issuedAt, Instant expiresAt) {
        return Jwts.builder()
                .id(UUID.randomUUID().toString())
                .issuer(securityProperties.getJwt().getIssuer())
                .subject(userProfile.metaAccountId())
                .claim(CLAIM_USER_ID, userProfile.userId())
                .claim(CLAIM_NICKNAME, userProfile.nickname())
                .claim(CLAIM_TOKEN_TYPE, tokenType)
                .issuedAt(Date.from(issuedAt))
                .expiration(Date.from(expiresAt))
                .signWith(secretKey, Jwts.SIG.HS512)
                .compact();
    }

    private SecretKey resolveSecretKey(SecurityProperties properties) {
        String configuredSecret = properties.getJwt().getSecret();
        if (StringUtils.hasText(configuredSecret)) {
            return secretKeyFromConfiguredValue(configuredSecret.trim());
        }

        if (!properties.getJwt().isAllowEphemeralSecret()) {
            throw new IllegalStateException("JWT 비밀키가 설정되지 않았습니다. MURANG_JWT_SECRET 환경 변수를 지정하세요.");
        }

        byte[] ephemeralBytes = new byte[HS512_MINIMUM_KEY_BYTES];
        secureRandom.nextBytes(ephemeralBytes);
        log.warn("JWT 비밀키가 없어 임시 메모리 키를 생성했습니다. 프로세스 재시작 시 기존 토큰은 모두 무효화됩니다.");
        return Keys.hmacShaKeyFor(ephemeralBytes);
    }

    private SecretKey secretKeyFromConfiguredValue(String configuredSecret) {
        byte[] keyBytes = tryDecodeBase64(configuredSecret);
        if (keyBytes == null) {
            keyBytes = configuredSecret.getBytes(StandardCharsets.UTF_8);
        }

        if (keyBytes.length < HS512_MINIMUM_KEY_BYTES) {
            throw new IllegalStateException("JWT 비밀키 길이가 부족합니다. 최소 64바이트 이상을 사용하세요.");
        }

        return Keys.hmacShaKeyFor(keyBytes);
    }

    private byte[] tryDecodeBase64(String value) {
        try {
            return Decoders.BASE64.decode(value);
        } catch (RuntimeException ignored) {
            return null;
        }
    }

    public record IssuedTokens(
            String accessToken,
            String refreshToken
    ) {
    }
}
