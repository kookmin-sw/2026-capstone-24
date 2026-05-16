package com.murang.auth.service;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.murang.MurangApplication;
import com.murang.auth.dto.MetaLoginRequest;
import com.murang.auth.dto.MetaLoginResponse;
import com.murang.user.domain.UserAccount;
import com.murang.user.repository.UserAccountRepository;
import java.time.Instant;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;

class AuthServicePersistenceTest {

    private static final String TEST_JWT_SECRET =
            "test-secret-for-hs512-signing-must-be-at-least-sixty-four-bytes-long-2026";

    @TempDir
    Path tempDir;

    @Test
    void loginPreservesPlayerIdAcrossApplicationRestarts() {
        String databaseUrl = "jdbc:h2:file:%s;MODE=MariaDB;DATABASE_TO_LOWER=TRUE;CASE_INSENSITIVE_IDENTIFIERS=TRUE"
                .formatted(tempDir.resolve("murang-persistence").toAbsolutePath().toString().replace('\\', '/'));

        MetaLoginResponse firstLogin = login(databaseUrl, "mock-meta:quest-user-31", "Restart User 31");
        MetaLoginResponse secondLogin = login(databaseUrl, "mock-meta:quest-user-31", "Restart User Renamed");

        assertEquals(firstLogin.user().playerId(), secondLogin.user().playerId());
        assertEquals("Restart User Renamed", secondLogin.user().nickname());
    }

    @Test
    void loginUpgradesLegacyUuidPlayerIdToUlid() {
        String databaseUrl = "jdbc:h2:file:%s;MODE=MariaDB;DATABASE_TO_LOWER=TRUE;CASE_INSENSITIVE_IDENTIFIERS=TRUE"
                .formatted(tempDir.resolve("murang-legacy-player-id").toAbsolutePath().toString().replace('\\', '/'));
        String legacyPlayerId = "550e8400-e29b-41d4-a716-446655440000";

        seedLegacyUser(databaseUrl, "quest-user-legacy", legacyPlayerId, "Legacy User");

        MetaLoginResponse upgraded = login(databaseUrl, "mock-meta:quest-user-legacy", "Legacy User");
        MetaLoginResponse relogin = login(databaseUrl, "mock-meta:quest-user-legacy", "Legacy User");

        assertNotEquals(legacyPlayerId, upgraded.user().playerId());
        assertTrue(upgraded.user().playerId().matches("^[0-9A-HJKMNP-TV-Z]{26}$"));
        assertEquals(upgraded.user().playerId(), relogin.user().playerId());
    }

    private MetaLoginResponse login(String databaseUrl, String metaIdToken, String nickname) {
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(MurangApplication.class)
                .profiles("test")
                .properties(
                        "spring.datasource.url=" + databaseUrl,
                        "spring.datasource.driver-class-name=org.h2.Driver",
                        "spring.datasource.username=sa",
                        "spring.datasource.password=",
                        "app.security.jwt.secret=" + TEST_JWT_SECRET,
                        "app.security.jwt.allow-ephemeral-secret=false"
                )
                .run()) {
            return context.getBean(AuthService.class)
                    .login(new MetaLoginRequest(metaIdToken, nickname));
        }
    }

    private void seedLegacyUser(String databaseUrl, String metaAccountId, String legacyPlayerId, String nickname) {
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(MurangApplication.class)
                .profiles("test")
                .properties(
                        "spring.datasource.url=" + databaseUrl,
                        "spring.datasource.driver-class-name=org.h2.Driver",
                        "spring.datasource.username=sa",
                        "spring.datasource.password=",
                        "app.security.jwt.secret=" + TEST_JWT_SECRET,
                        "app.security.jwt.allow-ephemeral-secret=false"
                )
                .run()) {
            UserAccountRepository repository = context.getBean(UserAccountRepository.class);
            repository.saveAndFlush(UserAccount.create(metaAccountId, legacyPlayerId, nickname, Instant.now()));
        }
    }
}
