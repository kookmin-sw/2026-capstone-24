package com.murang.room.runtime.ecs;

import static org.assertj.core.api.Assertions.assertThat;

import com.murang.room.runtime.NoopRoomRuntimeProvider;
import com.murang.room.runtime.RoomRuntimeProvider;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.ApplicationContext;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.TestPropertySource;

/**
 * Regression guard for the Spring DI wiring when both noop and ECS runtime
 * providers are present (aws-dev profile reproduction). The bug surfaced as
 * "expected single matching bean but found 2" because Spring's config-class
 * ordering is non-deterministic, so {@code NoopRoomRuntimeProvider}'s
 * {@code @ConditionalOnMissingBean} sometimes fired before the ECS bean was
 * registered. {@code @Primary} on the ECS bean resolves the duplicate at
 * injection time.
 */
@SpringBootTest
@ActiveProfiles("test")
@TestPropertySource(properties = {
        "murang.room.runtime.ecs.cluster=test-cluster",
        "murang.room.runtime.ecs.task-definition-family=test-family",
        "murang.room.runtime.ecs.subnet-ids=subnet-1",
        "murang.room.runtime.ecs.security-group-ids=sg-1",
        "murang.room.runtime.ecs.container-name=room-server",
        "murang.room.runtime.ecs.region=ap-northeast-2"
})
class EcsRoomRuntimeProviderWiringTest {

    @Autowired
    private RoomRuntimeProvider primaryProvider;

    @Autowired
    private ApplicationContext applicationContext;

    @Test
    void primaryProviderIsEcsWhenClusterPropertySet() {
        assertThat(primaryProvider).isInstanceOf(EcsRoomRuntimeProvider.class);
    }

    @Test
    void ecsBeanIsRegisteredAndPrimaryResolvesIt() {
        String[] beanNames = applicationContext.getBeanNamesForType(RoomRuntimeProvider.class);
        assertThat(beanNames).contains("ecsRoomRuntimeProvider");

        // noop may or may not be present depending on @ConditionalOnMissingBean evaluation
        // order; the important guarantee is that @Primary on ecsRoomRuntimeProvider routes
        // single-target injection to the ECS bean regardless.
        if (applicationContext.containsBean("noopRoomRuntimeProvider")) {
            Object noop = applicationContext.getBean("noopRoomRuntimeProvider");
            assertThat(noop).isInstanceOf(NoopRoomRuntimeProvider.class);
        }
    }
}
