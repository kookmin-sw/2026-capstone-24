package com.murang.room.runtime.ecs;

import com.murang.room.runtime.RoomRuntimeProvider;
import org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Primary;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.ecs.EcsClient;
import software.amazon.awssdk.services.ecs.EcsClientBuilder;

/**
 * Wires the ECS-backed {@link RoomRuntimeProvider} only when
 * {@code murang.room.runtime.ecs.cluster} is configured. In dev/test profiles
 * without ECS settings this configuration is skipped and the noop provider
 * from {@code RoomServerManagerConfiguration} remains active.
 *
 * <p>{@link #ecsRoomRuntimeProvider} is marked {@code @Primary} so that, even
 * when {@code NoopRoomRuntimeProvider}'s {@code @ConditionalOnMissingBean}
 * fires before this configuration is processed (Spring does not guarantee
 * config-class ordering), the ECS bean wins constructor injection into
 * {@code RoomServerManagerImpl}.
 */
@Configuration
@EnableConfigurationProperties(EcsRoomRuntimeProperties.class)
@ConditionalOnProperty(prefix = "murang.room.runtime.ecs", name = "cluster")
public class EcsRoomRuntimeConfiguration {

    @Bean(destroyMethod = "close")
    @ConditionalOnMissingBean
    public EcsClient ecsClient(EcsRoomRuntimeProperties properties) {
        EcsClientBuilder builder = EcsClient.builder();
        if (properties.region() != null && !properties.region().isBlank()) {
            builder.region(Region.of(properties.region()));
        }
        return builder.build();
    }

    @Bean
    @Primary
    public RoomRuntimeProvider ecsRoomRuntimeProvider(
            EcsClient ecsClient, EcsRoomRuntimeProperties properties) {
        return new EcsRoomRuntimeProvider(ecsClient, properties);
    }
}
