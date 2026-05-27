package com.murang.room.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.murang.common.exception.ApiException;
import com.murang.room.domain.RoomServerInstanceStatus;
import com.murang.room.manager.RoomProvisioningCommand;
import com.murang.room.manager.RoomServerSnapshot;
import java.util.Optional;
import static org.mockito.Mockito.when;
import com.murang.room.manager.RoomReadySignal;
import com.murang.room.manager.RoomServerManager;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class RoomInternalCallbackControllerTest {

    private static final String VALID_TOKEN = "test-internal-secret";
    private static final String READY_BODY = """
            {
              "taskPublicIp": "203.0.113.10",
              "gamePort": 7777,
              "roomRuntimeVersion": "v0.1.0"
            }
            """;

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private RoomServerManager roomServerManager;

    @Test
    void ready_withValidToken_returnsNoContentAndCallsManager() throws Exception {
        mockMvc.perform(post("/internal/rooms/42/ready")
                        .header(RoomInternalCallbackController.INTERNAL_TOKEN_HEADER, VALID_TOKEN)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(READY_BODY))
                .andExpect(status().isNoContent());

        verify(roomServerManager).notifyReady(eq(42L), any(RoomReadySignal.class));
    }

    @Test
    void ready_missingToken_returnsForbiddenAndDoesNotCallManager() throws Exception {
        mockMvc.perform(post("/internal/rooms/42/ready")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(READY_BODY))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("ROOM_INTERNAL_FORBIDDEN"));

        verify(roomServerManager, never()).notifyReady(anyLong(), any());
    }

    @Test
    void ready_wrongToken_returnsForbidden() throws Exception {
        mockMvc.perform(post("/internal/rooms/42/ready")
                        .header(RoomInternalCallbackController.INTERNAL_TOKEN_HEADER, "wrong")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(READY_BODY))
                .andExpect(status().isForbidden());

        verify(roomServerManager, never()).notifyReady(anyLong(), any());
    }

    @Test
    void heartbeat_withValidToken_returnsNoContent() throws Exception {
        mockMvc.perform(post("/internal/rooms/42/heartbeat")
                        .header(RoomInternalCallbackController.INTERNAL_TOKEN_HEADER, VALID_TOKEN))
                .andExpect(status().isNoContent());

        verify(roomServerManager).notifyHeartbeat(eq(42L), any());
    }

    @Test
    void heartbeat_missingToken_returnsForbidden() throws Exception {
        mockMvc.perform(post("/internal/rooms/42/heartbeat"))
                .andExpect(status().isForbidden());

        verify(roomServerManager, never()).notifyHeartbeat(anyLong(), any());
    }

    private static final String TERMINATE_BODY = "{\"reason\":\"last-player-left\"}";

    @Test
    void terminate_withValidToken_returnsNoContentAndCallsManager() throws Exception {
        RoomServerSnapshot snapshot = org.mockito.Mockito.mock(RoomServerSnapshot.class);
        when(snapshot.status()).thenReturn(RoomServerInstanceStatus.READY);
        when(roomServerManager.findByRoomId(42L)).thenReturn(Optional.of(snapshot));
        mockMvc.perform(post("/internal/rooms/42/terminate")
                        .header(RoomInternalCallbackController.INTERNAL_TOKEN_HEADER, VALID_TOKEN)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(TERMINATE_BODY))
                .andExpect(status().isNoContent());
        verify(roomServerManager).terminate(eq(42L), eq("ds-callback:last-player-left"));
    }

    @Test
    void terminate_missingToken_returnsForbidden() throws Exception {
        mockMvc.perform(post("/internal/rooms/42/terminate")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(TERMINATE_BODY))
                .andExpect(status().isForbidden());
        verify(roomServerManager, never()).terminate(anyLong(), any());
    }

    @Test
    void terminate_alreadyTerminated_returnsNoContentAndDoesNotCallManager() throws Exception {
        RoomServerSnapshot snapshot = org.mockito.Mockito.mock(RoomServerSnapshot.class);
        when(snapshot.status()).thenReturn(RoomServerInstanceStatus.TERMINATED);
        when(roomServerManager.findByRoomId(42L)).thenReturn(Optional.of(snapshot));
        mockMvc.perform(post("/internal/rooms/42/terminate")
                        .header(RoomInternalCallbackController.INTERNAL_TOKEN_HEADER, VALID_TOKEN)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(TERMINATE_BODY))
                .andExpect(status().isNoContent());
        verify(roomServerManager, never()).terminate(anyLong(), any());
    }

    @Test
    void terminate_persistentRoom_returnsNoContentAndDoesNotCallManager() throws Exception {
        RoomServerSnapshot snapshot = org.mockito.Mockito.mock(RoomServerSnapshot.class);
        when(snapshot.status()).thenReturn(RoomServerInstanceStatus.READY);
        when(snapshot.isPersistent()).thenReturn(true);
        when(roomServerManager.findByRoomId(42L)).thenReturn(Optional.of(snapshot));
        mockMvc.perform(post("/internal/rooms/42/terminate")
                        .header(RoomInternalCallbackController.INTERNAL_TOKEN_HEADER, VALID_TOKEN)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(TERMINATE_BODY))
                .andExpect(status().isNoContent());
        verify(roomServerManager, never()).terminate(anyLong(), any());
    }

    private static final String CREATE_PERSISTENT_BODY = """
            {
              "ownerUserId": 1,
              "photonSessionName": "demo-room-2026",
              "maxPlayers": 8,
              "roomRuntimeVersion": "v0.1.0"
            }
            """;

    @Test
    void createPersistent_withValidToken_returnsOkAndCallsManager() throws Exception {
        RoomServerSnapshot snapshot = org.mockito.Mockito.mock(RoomServerSnapshot.class);
        when(snapshot.roomId()).thenReturn(99L);
        when(snapshot.photonSessionName()).thenReturn("demo-room-2026");
        when(snapshot.maxPlayers()).thenReturn(8);
        when(snapshot.status()).thenReturn(RoomServerInstanceStatus.SERVER_STARTING);
        when(roomServerManager.provisionPersistent(any(RoomProvisioningCommand.class))).thenReturn(snapshot);

        mockMvc.perform(post("/internal/rooms/persistent")
                        .header(RoomInternalCallbackController.INTERNAL_TOKEN_HEADER, VALID_TOKEN)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(CREATE_PERSISTENT_BODY))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.data.roomId").value(99));

        verify(roomServerManager).provisionPersistent(any(RoomProvisioningCommand.class));
    }

    @Test
    void createPersistent_missingToken_returnsForbidden() throws Exception {
        mockMvc.perform(post("/internal/rooms/persistent")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(CREATE_PERSISTENT_BODY))
                .andExpect(status().isForbidden());

        verify(roomServerManager, never()).provisionPersistent(any());
    }

    @Test
    void createPersistent_conflict_returnsConflict() throws Exception {
        when(roomServerManager.provisionPersistent(any(RoomProvisioningCommand.class)))
                .thenThrow(ApiException.persistentRoomAlreadyExists());

        mockMvc.perform(post("/internal/rooms/persistent")
                        .header(RoomInternalCallbackController.INTERNAL_TOKEN_HEADER, VALID_TOKEN)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(CREATE_PERSISTENT_BODY))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("PERSISTENT_ROOM_ALREADY_EXISTS"));
    }
}
