package com.murang.room.repository;

import com.murang.room.domain.Room;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface RoomRepository extends JpaRepository<Room, Long> {

    Optional<Room> findByPhotonSessionName(String photonSessionName);

    List<Room> findAllByClosedAtIsNull();

    Optional<Room> findFirstByIsPersistentTrueAndClosedAtIsNull();
}
