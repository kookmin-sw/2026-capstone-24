package com.murang.room.runtime.ecs;

import java.util.List;
import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * Configuration for the ECS Fargate {@link com.murang.room.runtime.RoomRuntimeProvider}
 * implementation. Activated by {@code EcsRoomRuntimeConfiguration} only when
 * {@code cluster} is set, so dev/test profiles without ECS leave the bean
 * undefined and fall back to {@code NoopRoomRuntimeProvider}.
 *
 * <p>Required fields:
 * <ul>
 *   <li>{@code region} — AWS region (e.g. {@code ap-northeast-2}).</li>
 *   <li>{@code cluster} — ECS cluster name or ARN.</li>
 *   <li>{@code taskDefinitionFamily} — task definition family or full ARN; AWS
 *   resolves to the latest revision when only the family is supplied.</li>
 *   <li>{@code subnetIds} — subnets to place Fargate tasks in.</li>
 *   <li>{@code securityGroupIds} — security groups to attach.</li>
 *   <li>{@code containerName} — container name inside the task definition
 *   that receives the room env vars (RunTask container override target).</li>
 * </ul>
 *
 * <p>Defaults:
 * <ul>
 *   <li>{@code assignPublicIp} = {@code true} (dev: Fargate task needs public
 *   IP so Quest clients can reach {@code public_ip:game_port/udp}).</li>
 * </ul>
 */
@ConfigurationProperties(prefix = "murang.room.runtime.ecs")
public record EcsRoomRuntimeProperties(
        String region,
        String cluster,
        String taskDefinitionFamily,
        List<String> subnetIds,
        List<String> securityGroupIds,
        Boolean assignPublicIp,
        String containerName
) {

    public boolean assignPublicIpOrDefault() {
        return assignPublicIp == null || assignPublicIp;
    }
}
