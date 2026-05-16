package com.murang.room.observability;

import org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import software.amazon.awssdk.services.cloudwatch.CloudWatchAsyncClient;

/**
 * Provides a {@link CloudWatchAsyncClient} for the Micrometer
 * {@code micrometer-registry-cloudwatch2} adapter so Spring's
 * {@code CloudWatchMetricsExportAutoConfiguration} can publish meters to
 * CloudWatch when {@code management.cloudwatch.metrics.export.enabled=true}.
 * The bean uses the AWS SDK default credential and region providers so it
 * picks up the EC2 instance profile and metadata in the aws-dev topology.
 */
@Configuration
@ConditionalOnProperty(
        prefix = "management.cloudwatch.metrics.export",
        name = "enabled",
        havingValue = "true"
)
public class CloudWatchMetricsConfiguration {

    @Bean(destroyMethod = "close")
    @ConditionalOnMissingBean
    public CloudWatchAsyncClient cloudWatchAsyncClient() {
        return CloudWatchAsyncClient.builder().build();
    }
}
