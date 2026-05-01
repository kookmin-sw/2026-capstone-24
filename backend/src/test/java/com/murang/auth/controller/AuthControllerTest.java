package com.murang.auth.controller;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.security.Keys;
import java.nio.charset.StandardCharsets;
import java.util.Date;
import java.util.UUID;
import javax.crypto.SecretKey;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class AuthControllerTest {

    private static final String TEST_JWT_SECRET =
            "test-secret-for-hs512-signing-must-be-at-least-sixty-four-bytes-long-2026";

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @Test
    void metaLoginReturnsTokensForValidMockToken() throws Exception {
        mockMvc.perform(post("/api/v1/auth/meta-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "metaIdToken": "mock-meta:quest-user-01",
                                  "nickname": "Murang User 01"
                                }
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.data.accessToken").isNotEmpty())
                .andExpect(jsonPath("$.data.refreshToken").isNotEmpty())
                .andExpect(jsonPath("$.data.user.playerId").isNotEmpty())
                .andExpect(jsonPath("$.data.user.nickname").value("Murang User 01"));
    }

    @Test
    void metaLoginRejectsUnexpectedTokenFormat() throws Exception {
        mockMvc.perform(post("/api/v1/auth/meta-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "metaIdToken": "invalid-token",
                                  "nickname": "Murang User"
                                }
                                """))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("AUTH_INVALID_META_TOKEN"));
    }

    @Test
    void metaLoginRejectsDuplicateNicknameFromAnotherMetaAccount() throws Exception {
        login("mock-meta:quest-user-11", "Shared Nickname");

        mockMvc.perform(post("/api/v1/auth/meta-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "metaIdToken": "mock-meta:quest-user-12",
                                  "nickname": "Shared Nickname"
                                }
                                """))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("AUTH_NICKNAME_DUPLICATE"));
    }

    @Test
    void metaLoginReusesPlayerIdForSameMetaAccountAfterNicknameChange() throws Exception {
        TokenBundle firstLogin = login("mock-meta:quest-user-17", "Original Nickname");
        TokenBundle secondLogin = login("mock-meta:quest-user-17", "Renamed Nickname");

        org.junit.jupiter.api.Assertions.assertEquals(firstLogin.playerId(), secondLogin.playerId());
        org.junit.jupiter.api.Assertions.assertEquals("Renamed Nickname", secondLogin.nickname());
    }

    @Test
    void metaLoginRejectsNicknameWithUnsupportedCharacters() throws Exception {
        mockMvc.perform(post("/api/v1/auth/meta-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "metaIdToken": "mock-meta:quest-user-13",
                                  "nickname": "Bad*Nickname"
                                }
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("VALIDATION_REQUEST"));
    }

    @Test
    void metaLoginRejectsNicknameLongerThanThirtyTwoCharacters() throws Exception {
        mockMvc.perform(post("/api/v1/auth/meta-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "metaIdToken": "mock-meta:quest-user-14",
                                  "nickname": "123456789012345678901234567890123"
                                }
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("VALIDATION_REQUEST"));
    }

    @Test
    void refreshReturnsNewTokensForValidRefreshToken() throws Exception {
        TokenBundle login = login("mock-meta:quest-user-15", "Refresh User 15");

        TokenBundle refreshed = refresh(login.refreshToken());

        org.junit.jupiter.api.Assertions.assertNotEquals(login.accessToken(), refreshed.accessToken());
        org.junit.jupiter.api.Assertions.assertNotEquals(login.refreshToken(), refreshed.refreshToken());
        org.junit.jupiter.api.Assertions.assertEquals(login.playerId(), refreshed.playerId());
        org.junit.jupiter.api.Assertions.assertEquals(login.nickname(), refreshed.nickname());
    }

    @Test
    void refreshAcceptsLegacyRefreshTokenSubjectWithMetaAccountId() throws Exception {
        TokenBundle login = login("mock-meta:quest-user-18", "Legacy Refresh User");
        TokenBundle refreshed = refresh(buildLegacyRefreshToken("quest-user-18"));

        org.junit.jupiter.api.Assertions.assertEquals(login.playerId(), refreshed.playerId());
        org.junit.jupiter.api.Assertions.assertEquals(login.nickname(), refreshed.nickname());
    }

    @Test
    void refreshRejectsAccessTokenInRefreshTokenField() throws Exception {
        TokenBundle login = login("mock-meta:quest-user-16", "Refresh User 16");

        mockMvc.perform(post("/api/v1/auth/refresh")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "refreshToken": "%s"
                                }
                                """.formatted(login.accessToken())))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("AUTH_INVALID_JWT"));
    }

    @Test
    void refreshRejectsMalformedToken() throws Exception {
        mockMvc.perform(post("/api/v1/auth/refresh")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "refreshToken": "invalid-token"
                                }
                                """))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("AUTH_INVALID_JWT"));
    }

    private TokenBundle login(String metaIdToken, String nickname) throws Exception {
        String content = mockMvc.perform(post("/api/v1/auth/meta-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "metaIdToken": "%s",
                                  "nickname": "%s"
                                }
                                """.formatted(metaIdToken, nickname)))
                .andExpect(status().isOk())
                .andReturn()
                .getResponse()
                .getContentAsString();

        return readTokenBundle(content);
    }

    private TokenBundle refresh(String refreshToken) throws Exception {
        String content = mockMvc.perform(post("/api/v1/auth/refresh")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "refreshToken": "%s"
                                }
                                """.formatted(refreshToken)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.data.accessToken").isNotEmpty())
                .andExpect(jsonPath("$.data.refreshToken").isNotEmpty())
                .andReturn()
                .getResponse()
                .getContentAsString();

        return readTokenBundle(content);
    }

    private TokenBundle readTokenBundle(String content) throws Exception {
        JsonNode data = objectMapper.readTree(content).path("data");
        return new TokenBundle(
                data.path("accessToken").asText(),
                data.path("refreshToken").asText(),
                data.path("user").path("playerId").asText(),
                data.path("user").path("nickname").asText()
        );
    }

    private String buildLegacyRefreshToken(String metaAccountId) {
        SecretKey secretKey = Keys.hmacShaKeyFor(TEST_JWT_SECRET.getBytes(StandardCharsets.UTF_8));
        Date now = new Date();
        Date expiresAt = new Date(now.getTime() + 86_400_000L);

        return Jwts.builder()
                .id(UUID.randomUUID().toString())
                .issuer("murang-backend")
                .subject(metaAccountId)
                .claim("userId", 1L)
                .claim("nickname", "Legacy Refresh User")
                .claim("tokenType", "refresh")
                .issuedAt(now)
                .expiration(expiresAt)
                .signWith(secretKey, Jwts.SIG.HS512)
                .compact();
    }

    private record TokenBundle(
            String accessToken,
            String refreshToken,
            String playerId,
            String nickname
    ) {
    }
}
