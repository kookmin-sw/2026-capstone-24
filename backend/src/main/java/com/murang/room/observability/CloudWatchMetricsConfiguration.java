package com.murang.room.observability;

import io.micrometer.cloudwatch2.CloudWatchConfig;
import io.micrometer.cloudwatch2.CloudWatchMeterRegistry;
import io.micrometer.core.instrument.Clock;
import org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.env.Environment;
import software.amazon.awssdk.services.cloudwatch.CloudWatchAsyncClient;

/**
 * Spring Boot 3.4 ships without a bundled {@code CloudWatchMetricsExportAutoConfiguration},
 * so the {@link CloudWatchMeterRegistry} is wired manually whenever
 * {@code management.cloudwatch.metrics.export.enabled=true}. The registry is
 * exposed as a regular {@code MeterRegistry} bean and Spring Boot's
 * {@code CompositeMeterRegistryAutoConfiguration} merges it with the default
 * {@code SimpleMeterRegistry}, so meters created against {@code MeterRegistry}
 * (e.g. {@code active_room_count}) are also published to CloudWatch.
 */
@Configuration
@ConditionalOnProperty(
        prefix = "management.cloudwatch.metrics.export",
        name = "enabled",
        havingValue = "true"
)
public class CloudWatchMetricsConfiguration {

    private static final String PROPERTY_PREFIX = "management.cloudwatch.metrics.export.";
    private static final String MICROMETER_KEY_PREFIX = "cloudwatch.";

    @Bean(destroyMethod = "close")
    @ConditionalOnMissingBean
    public CloudWatchAsyncClient cloudWatchAsyncClient() {
        return CloudWatchAsyncClient.builder().build();
    }

    @Bean
    @ConditionalOnMissingBean
    public CloudWatchConfig cloudWatchConfig(Environment environment) {
        return key -> {
            String suffix = key.startsWith(MICROMETER_KEY_PREFIX)
                    ? key.substring(MICROMETER_KEY_PREFIX.length())
                    : key;
            return environment.getProperty(PROPERTY_PREFIX + suffix);
        };
    }

    @Bean(destroyMethod = "close")
    @ConditionalOnMissingBean
    public CloudWatchMeterRegistry cloudWatchMeterRegistry(
            CloudWatchConfig config,
            CloudWatchAsyncClient client
    ) {
        return new CloudWatchMeterRegistry(config, Clock.SYSTEM, client);
    }
}
