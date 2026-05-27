package com.murang.room.domain;

import static org.assertj.core.api.Assertions.assertThat;

import java.time.Instant;
import org.junit.jupiter.api.Test;

class RoomTest {

    @Test
    void open_setsIsPersistentFalse() {
        Room room = Room.open(1L, "session-normal", 8, null, Instant.now());
        assertThat(room.isPersistent()).isFalse();
    }

    @Test
    void openPersistent_setsIsPersistentTrue() {
        Room room = Room.openPersistent(1L, "session-demo", 8, null, Instant.now());
        assertThat(room.isPersistent()).isTrue();
    }
}
