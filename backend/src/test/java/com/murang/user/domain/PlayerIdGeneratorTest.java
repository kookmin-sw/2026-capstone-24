package com.murang.user.domain;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.Test;

class PlayerIdGeneratorTest {

    @Test
    void newPlayerId_ReturnsValidUlid() {
        String playerId = PlayerIdGenerator.newPlayerId();

        assertEquals(26, playerId.length());
        assertTrue(PlayerIdGenerator.isUlid(playerId));
    }

    @Test
    void isUlid_RejectsLegacyUuid() {
        assertFalse(PlayerIdGenerator.isUlid("550e8400-e29b-41d4-a716-446655440000"));
    }
}
