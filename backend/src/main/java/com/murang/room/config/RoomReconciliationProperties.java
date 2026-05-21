package com.murang.room.config;

import java.time.Duration;
import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * Reconciliation scheduler timing configuration.
 * prefix: {@code murang.room.reconciliation}
 *
 * <ul>
 *   <li>{@code heartbeat-timeout} – max silence before READY/ACTIVE instance is considered dead
 *       (default 90s = 3 × heartbeat interval)</li>
 *   <li>{@code provisioning-timeout} – max time a PROVISIONING/SERVER_STARTING instance may
 *       stay without reaching READY (default 5m)</li>
 *   <li>{@code scan-interval} – fixed-delay between reconciliation scans (default 30s)</li>
 * </ul>
 */
@ConfigurationProperties(prefix = "murang.room.reconciliation")
public record RoomReconciliationProperties(
        Duration heartbeatTimeout,
        Duration provisioningTimeout,
        Duration scanInterval
) {
    public RoomReconciliationProperties {
        if (heartbeatTimeout == null) heartbeatTimeout = Duration.ofSeconds(90);
        if (provisioningTimeout == null) provisioningTimeout = Duration.ofMinutes(5);
        if (scanInterval == null) scanInterval = Duration.ofSeconds(30);
    }
}
