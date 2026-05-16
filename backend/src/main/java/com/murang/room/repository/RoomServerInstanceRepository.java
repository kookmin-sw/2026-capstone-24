package com.murang.room.repository;

import com.murang.room.domain.RoomServerInstance;
import com.murang.room.domain.RoomServerInstanceStatus;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface RoomServerInstanceRepository extends JpaRepository<RoomServerInstance, Long> {

    Optional<RoomServerInstance> findByRoomId(Long roomId);

    Optional<RoomServerInstance> findByEcsTaskArn(String ecsTaskArn);

    List<RoomServerInstance> findAllByStatusIn(List<RoomServerInstanceStatus> statuses);

    List<RoomServerInstance> findAllByStatusInAndLastHeartbeatAtBefore(
            List<RoomServerInstanceStatus> statuses, Instant threshold);
}
