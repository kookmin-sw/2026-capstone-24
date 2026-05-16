package com.murang.room.controller.dto;

import com.murang.room.domain.RoomServerInstanceStatus;
import com.murang.room.manager.RoomServerSnapshot;
import java.time.Instant;

/**
 * Spring → Client 룸 상태 응답 body. POST /api/v1/rooms 생성 응답과
 * GET /api/v1/rooms/{id} 조회 응답이 같은 모양을 공유한다.
 *
 * <p>{@code taskPublicIp}/{@code gamePort} 는 ready callback 도달 전까지
 * null. {@code readyAt} 도 마찬가지. 클라이언트는 status=READY 와
 * taskPublicIp/gamePort 모두 채워진 시점에 Photon 합류를 시도한다.
 */
public record RoomResponse(
        Long roomId,
        String photonSessionName,
        int maxPlayers,
        boolean locked,
        RoomServerInstanceStatus status,
        String taskPublicIp,
        Integer gamePort,
        Instant createdAt,
        Instant readyAt
) {

    public static RoomResponse of(RoomServerSnapshot snapshot) {
        return new RoomResponse(
                snapshot.roomId(),
                snapshot.photonSessionName(),
                snapshot.maxPlayers(),
                snapshot.locked(),
                snapshot.status(),
                snapshot.taskPublicIp(),
                snapshot.gamePort(),
                snapshot.createdAt(),
                snapshot.readyAt()
        );
    }
}
