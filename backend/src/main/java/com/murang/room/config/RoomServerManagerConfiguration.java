package com.murang.room.config;

import com.murang.room.runtime.NoopRoomRuntimeProvider;
import com.murang.room.runtime.RoomRuntimeProvider;
import java.time.Clock;
import org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.scheduling.annotation.EnableScheduling;

@Configuration
@EnableScheduling
@EnableConfigurationProperties(RoomInternalCallbackProperties.class)
public class RoomServerManagerConfiguration {

    @Bean
    @ConditionalOnMissingBean
    public Clock roomServerClock() {
        return Clock.systemUTC();
    }

    @Bean
    @ConditionalOnMissingBean(RoomRuntimeProvider.class)
    public RoomRuntimeProvider noopRoomRuntimeProvider() {
        return new NoopRoomRuntimeProvider();
    }
}
