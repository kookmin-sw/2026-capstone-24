package com.murang.room.config;

import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * Configuration for the internal ready/heartbeat callbacks that dedicated room
 * servers POST back to Spring. {@code base-url} is the publicly addressable URL
 * (EC2 public DNS in aws-dev) that Fargate tasks dial; {@code shared-secret} is
 * a token compared against {@code X-Internal-Token} on inbound callback
 * requests.
 */
@ConfigurationProperties(prefix = "murang.room.internal-callback")
public record RoomInternalCallbackProperties(
        String baseUrl,
        String sharedSecret
) {
}
