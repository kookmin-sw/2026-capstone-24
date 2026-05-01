package com.murang.user.controller;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;

@SpringBootTest(properties = "app.security.jwt.access-token-ttl=PT1S")
@AutoConfigureMockMvc
@ActiveProfiles("test")
class UserControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @Test
    void meReturnsProfileForValidAccessToken() throws Exception {
        LoginBundle login = login("mock-meta:quest-user-21", "Profile User 21");

                mockMvc.perform(get("/api/v1/users/me")
                        .header(HttpHeaders.AUTHORIZATION, "Bearer " + login.accessToken()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.data.playerId").value(login.playerId()))
                .andExpect(jsonPath("$.data.metaAccountId").value("quest-user-21"))
                .andExpect(jsonPath("$.data.nickname").value("Profile User 21"));
    }

    @Test
    void meRejectsMissingToken() throws Exception {
        mockMvc.perform(get("/api/v1/users/me"))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("AUTH_INVALID_JWT"));
    }

    @Test
    void meRejectsInvalidToken() throws Exception {
        mockMvc.perform(get("/api/v1/users/me")
                        .header(HttpHeaders.AUTHORIZATION, "Bearer invalid-token"))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("AUTH_INVALID_JWT"));
    }

    @Test
    void meRejectsExpiredToken() throws Exception {
        LoginBundle login = login("mock-meta:quest-user-22", "Expired User 22");

        Thread.sleep(1500L);

        mockMvc.perform(get("/api/v1/users/me")
                        .header(HttpHeaders.AUTHORIZATION, "Bearer " + login.accessToken()))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("AUTH_INVALID_JWT"));
    }

    private LoginBundle login(String metaIdToken, String nickname) throws Exception {
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

        JsonNode data = objectMapper.readTree(content).path("data");
        return new LoginBundle(
                data.path("accessToken").asText(),
                data.path("user").path("playerId").asText()
        );
    }

    private record LoginBundle(
            String accessToken,
            String playerId
    ) {
    }
}
