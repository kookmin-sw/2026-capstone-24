package com.murang.auth.controller;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

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

    @Autowired
    private MockMvc mockMvc;

    @Test
    void metaLoginReturnsTokensForValidMockToken() throws Exception {
        mockMvc.perform(post("/api/v1/auth/meta-login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "metaIdToken": "mock-meta:quest-user-01",
                                  "nickname": "Murang User"
                                }
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.data.accessToken").isNotEmpty())
                .andExpect(jsonPath("$.data.refreshToken").isNotEmpty())
                .andExpect(jsonPath("$.data.user.userId").value(1))
                .andExpect(jsonPath("$.data.user.nickname").value("Murang User"));
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
}
