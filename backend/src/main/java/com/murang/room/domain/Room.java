package com.murang.room.domain;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import java.time.Instant;

@Entity
@Table(
        name = "rooms",
        uniqueConstraints = {
                @UniqueConstraint(name = "uk_rooms_photon_session_name", columnNames = "photon_session_name")
        }
)
public class Room {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "room_id", nullable = false)
    private Long roomId;

    @Column(name = "owner_user_id", nullable = false, updatable = false)
    private Long ownerUserId;

    @Column(name = "photon_session_name", nullable = false, length = 128, updatable = false)
    private String photonSessionName;

    @Column(name = "max_players", nullable = false)
    private int maxPlayers;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Column(name = "closed_at")
    private Instant closedAt;

    protected Room() {
    }

    public static Room open(Long ownerUserId, String photonSessionName, int maxPlayers, Instant now) {
        Room room = new Room();
        room.ownerUserId = ownerUserId;
        room.photonSessionName = photonSessionName;
        room.maxPlayers = maxPlayers;
        room.createdAt = now;
        return room;
    }

    public void close(Instant now) {
        if (closedAt == null) {
            closedAt = now;
        }
    }

    public Long getRoomId() {
        return roomId;
    }

    public Long getOwnerUserId() {
        return ownerUserId;
    }

    public String getPhotonSessionName() {
        return photonSessionName;
    }

    public int getMaxPlayers() {
        return maxPlayers;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public Instant getClosedAt() {
        return closedAt;
    }

    public boolean isClosed() {
        return closedAt != null;
    }
}
