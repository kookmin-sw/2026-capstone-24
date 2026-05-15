package com.murang.room.controller.dto;

import com.murang.room.domain.RoomServerInstanceStatus;
import com.murang.room.manager.RoomServerSnapshot;
import java.time.Instant;

/**
 * Spring → Client 룸 생성 응답 body.
 *
 * <p>{@code status} 는 응답 시점의 {@link RoomServerInstanceStatus} (대부분
 * {@code SERVER_STARTING}). {@code taskPublicIp}/{@code gamePort} 는 ready
 * callback 도달 전까지 null. 클라이언트는 별도 조회 endpoint 또는 Photon
 * Lobby SessionList 갱신으로 READY 진입을 감지한다.
 */
public record RoomCreateResponse(
        Long roomId,
        String photonSessionName,
        int maxPlayers,
        boolean locked,
        RoomServerInstanceStatus status,
        String taskPublicIp,
        Integer gamePort,
        Instant createdAt
) {

    public static RoomCreateResponse of(RoomServerSnapshot snapshot) {
        return new RoomCreateResponse(
                snapshot.roomId(),
                snapshot.photonSessionName(),
                snapshot.maxPlayers(),
                snapshot.locked(),
                snapshot.status(),
                snapshot.taskPublicIp(),
                snapshot.gamePort(),
                snapshot.createdAt()
        );
    }
}
