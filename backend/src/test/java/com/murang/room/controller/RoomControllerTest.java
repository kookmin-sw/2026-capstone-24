package com.murang.room.controller;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.murang.room.domain.RoomServerInstanceStatus;
import com.murang.room.repository.RoomRepository;
import com.murang.room.repository.RoomServerInstanceRepository;
import com.murang.room.runtime.NoopRoomRuntimeProvider;
import com.murang.room.runtime.RoomRuntimeProvider;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Primary;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class RoomControllerTest {

    @TestConfiguration
    static class TestRuntimeConfig {

        @Bean
        @Primary
        RoomRuntimeProvider noopRuntime() {
            return new NoopRoomRuntimeProvider();
        }
    }

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @Autowired
    private RoomRepository roomRepository;

    @Autowired
    private RoomServerInstanceRepository instanceRepository;

    @Test
    void createRoom_withValidPayloadAndAuth_returnsOkAndPersistsRows() throws Exception {
        String accessToken = loginAndGetAccessToken("mock-meta:room-host-1", "RoomHost1");
        String sessionName = uniqueSessionName();

        String responseBody = mockMvc.perform(post("/api/v1/rooms")
                        .header(HttpHeaders.AUTHORIZATION, "Bearer " + accessToken)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "photonSessionName": "%s",
                                  "maxPlayers": 8,
                                  "passwordHash": null,
                                  "roomRuntimeVersion": "v0.1.0"
                                }
                                """.formatted(sessionName)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.data.roomId").isNumber())
                .andExpect(jsonPath("$.data.photonSessionName").value(sessionName))
                .andExpect(jsonPath("$.data.maxPlayers").value(8))
                .andExpect(jsonPath("$.data.locked").value(false))
                .andExpect(jsonPath("$.data.status").value("SERVER_STARTING"))
                .andExpect(jsonPath("$.data.taskPublicIp").doesNotExist())
                .andExpect(jsonPath("$.data.gamePort").doesNotExist())
                .andReturn()
                .getResponse()
                .getContentAsString();

        JsonNode data = objectMapper.readTree(responseBody).path("data");
        Long roomId = data.path("roomId").asLong();

        assertThat(roomRepository.findById(roomId)).isPresent().get().satisfies(room -> {
            assertThat(room.getPhotonSessionName()).isEqualTo(sessionName);
            assertThat(room.getMaxPlayers()).isEqualTo(8);
            assertThat(room.getPasswordHash()).isNull();
            assertThat(room.isLocked()).isFalse();
            assertThat(room.getClosedAt()).isNull();
        });
        assertThat(instanceRepository.findByRoomId(roomId)).isPresent().get().satisfies(instance -> {
            assertThat(instance.getStatus()).isEqualTo(RoomServerInstanceStatus.SERVER_STARTING);
            assertThat(instance.getEcsTaskArn()).startsWith("noop:task:");
        });
    }

    @Test
    void createRoom_withPasswordHash_marksRoomLocked() throws Exception {
        String accessToken = loginAndGetAccessToken("mock-meta:room-host-2", "RoomHost2");
        String sessionName = uniqueSessionName();

        mockMvc.perform(post("/api/v1/rooms")
                        .header(HttpHeaders.AUTHORIZATION, "Bearer " + accessToken)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "photonSessionName": "%s",
                                  "maxPlayers": 4,
                                  "passwordHash": "abc123hash",
                                  "roomRuntimeVersion": "v0.1.0"
                                }
                                """.formatted(sessionName)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.data.locked").value(true));
    }

    @Test
    void createRoom_duplicatePhotonSessionName_returns409() throws Exception {
        String accessToken = loginAndGetAccessToken("mock-meta:room-host-3", "RoomHost3");
        String sessionName = uniqueSessionName();

        mockMvc.perform(post("/api/v1/rooms")
                        .header(HttpHeaders.AUTHORIZATION, "Bearer " + accessToken)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "photonSessionName": "%s",
                                  "maxPlayers": 4,
                                  "roomRuntimeVersion": "v0.1.0"
                                }
                                """.formatted(sessionName)))
                .andExpect(status().isOk());

        mockMvc.perform(post("/api/v1/rooms")
                        .header(HttpHeaders.AUTHORIZATION, "Bearer " + accessToken)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "photonSessionName": "%s",
                                  "maxPlayers": 8,
                                  "roomRuntimeVersion": "v0.1.0"
                                }
                                """.formatted(sessionName)))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("ROOM_NAME_DUPLICATE"));
    }

    @Test
    void createRoom_missingAuth_returns401() throws Exception {
        mockMvc.perform(post("/api/v1/rooms")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "photonSessionName": "%s",
                                  "maxPlayers": 4,
                                  "roomRuntimeVersion": "v0.1.0"
                                }
                                """.formatted(uniqueSessionName())))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("AUTH_INVALID_JWT"));
    }

    @Test
    void createRoom_invalidPayload_returns400() throws Exception {
        String accessToken = loginAndGetAccessToken("mock-meta:room-host-4", "RoomHost4");

        mockMvc.perform(post("/api/v1/rooms")
                        .header(HttpHeaders.AUTHORIZATION, "Bearer " + accessToken)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "photonSessionName": "",
                                  "maxPlayers": 0,
                                  "roomRuntimeVersion": ""
                                }
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    void createRoom_invalidPhotonSessionNameCharacters_returns400() throws Exception {
        String accessToken = loginAndGetAccessToken("mock-meta:room-host-5", "RoomHost5");

        mockMvc.perform(post("/api/v1/rooms")
                        .header(HttpHeaders.AUTHORIZATION, "Bearer " + accessToken)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "photonSessionName": "room with spaces",
                                  "maxPlayers": 4,
                                  "roomRuntimeVersion": "v0.1.0"
                                }
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    void createRoom_maxPlayersOverLimit_returns400() throws Exception {
        String accessToken = loginAndGetAccessToken("mock-meta:room-host-6", "RoomHost6");

        mockMvc.perform(post("/api/v1/rooms")
                        .header(HttpHeaders.AUTHORIZATION, "Bearer " + accessToken)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "photonSessionName": "%s",
                                  "maxPlayers": 999,
                                  "roomRuntimeVersion": "v0.1.0"
                                }
                                """.formatted(uniqueSessionName())))
                .andExpect(status().isBadRequest());
    }

    private String loginAndGetAccessToken(String metaIdToken, String nickname) throws Exception {
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

        return objectMapper.readTree(content).path("data").path("accessToken").asText();
    }

    private String uniqueSessionName() {
        return "room-" + UUID.randomUUID().toString().substring(0, 8);
    }
}
