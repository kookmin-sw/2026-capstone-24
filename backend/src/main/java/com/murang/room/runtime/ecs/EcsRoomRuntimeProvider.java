package com.murang.room.runtime.ecs;

import com.murang.room.config.RoomInternalCallbackProperties;
import com.murang.room.runtime.ProvisionedRoomTask;
import com.murang.room.runtime.RoomRuntimeProvider;
import com.murang.room.runtime.RoomRuntimeProviderException;
import com.murang.room.runtime.RoomTaskRuntimeState;
import com.murang.room.runtime.RoomTaskStartRequest;
import com.murang.room.runtime.RoomTaskStopRequest;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import java.util.stream.Collectors;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import software.amazon.awssdk.services.ecs.EcsClient;
import software.amazon.awssdk.services.ecs.model.AssignPublicIp;
import software.amazon.awssdk.services.ecs.model.AwsVpcConfiguration;
import software.amazon.awssdk.services.ecs.model.ContainerOverride;
import software.amazon.awssdk.services.ecs.model.DescribeTasksRequest;
import software.amazon.awssdk.services.ecs.model.DescribeTasksResponse;
import software.amazon.awssdk.services.ecs.model.EcsException;
import software.amazon.awssdk.services.ecs.model.Failure;
import software.amazon.awssdk.services.ecs.model.KeyValuePair;
import software.amazon.awssdk.services.ecs.model.LaunchType;
import software.amazon.awssdk.services.ecs.model.NetworkConfiguration;
import software.amazon.awssdk.services.ecs.model.RunTaskRequest;
import software.amazon.awssdk.services.ecs.model.RunTaskResponse;
import software.amazon.awssdk.services.ecs.model.StopTaskRequest;
import software.amazon.awssdk.services.ecs.model.Task;
import software.amazon.awssdk.services.ecs.model.TaskOverride;

/**
 * {@link RoomRuntimeProvider} implementation that orchestrates Unity Headless
 * Dedicated Server tasks on ECS Fargate via {@code RunTask} / {@code StopTask}
 * / {@code DescribeTasks}. Environment overrides on the container deliver the
 * room context (ID, session name, callback URLs, runtime version) so the
 * dedicated server can POST back to {@code /internal/rooms/{id}/ready} and
 * {@code /heartbeat}.
 */
public class EcsRoomRuntimeProvider implements RoomRuntimeProvider {

    private static final Logger log = LoggerFactory.getLogger(EcsRoomRuntimeProvider.class);

    static final String ENV_ROOM_ID = "ROOM_ID";
    static final String ENV_PHOTON_SESSION_NAME = "PHOTON_SESSION_NAME";
    static final String ENV_MAX_PLAYERS = "MAX_PLAYERS";
    static final String ENV_ROOM_RUNTIME_VERSION = "ROOM_RUNTIME_VERSION";
    static final String ENV_READY_CALLBACK_URL = "ROOM_READY_CALLBACK_URL";
    static final String ENV_HEARTBEAT_CALLBACK_URL = "ROOM_HEARTBEAT_CALLBACK_URL";
    static final String ENV_INTERNAL_CALLBACK_SHARED_SECRET = "MURANG_ROOM_INTERNAL_CALLBACK_SHARED_SECRET";

    private final EcsClient ecsClient;
    private final EcsRoomRuntimeProperties properties;
    private final RoomInternalCallbackProperties callbackProperties;

    public EcsRoomRuntimeProvider(
            EcsClient ecsClient,
            EcsRoomRuntimeProperties properties,
            RoomInternalCallbackProperties callbackProperties
    ) {
        this.ecsClient = ecsClient;
        this.properties = properties;
        this.callbackProperties = callbackProperties;
    }

    @Override
    public ProvisionedRoomTask startRoomTask(RoomTaskStartRequest request) {
        RunTaskRequest runRequest = RunTaskRequest.builder()
                .cluster(properties.cluster())
                .taskDefinition(properties.taskDefinitionFamily())
                .launchType(LaunchType.FARGATE)
                .count(1)
                .networkConfiguration(networkConfiguration())
                .overrides(taskOverride(request))
                .build();

        RunTaskResponse response;
        try {
            response = ecsClient.runTask(runRequest);
        } catch (EcsException ex) {
            throw new RoomRuntimeProviderException(
                    "ECS RunTask 호출이 실패했습니다: " + ex.awsErrorDetails().errorMessage(), ex);
        }

        if (response.failures() != null && !response.failures().isEmpty()) {
            String reasons = response.failures().stream()
                    .map(EcsRoomRuntimeProvider::formatFailure)
                    .collect(Collectors.joining(", "));
            throw new RoomRuntimeProviderException("ECS RunTask 실패 보고: " + reasons);
        }

        List<Task> tasks = response.tasks();
        if (tasks == null || tasks.isEmpty()) {
            throw new RoomRuntimeProviderException("ECS RunTask 가 task 를 반환하지 않았습니다.");
        }

        Task task = tasks.get(0);
        log.info("ECS RunTask submitted room_id={} cluster_arn={} task_arn={}",
                request.roomId(), task.clusterArn(), task.taskArn());
        return new ProvisionedRoomTask(task.clusterArn(), task.taskArn());
    }

    @Override
    public void stopRoomTask(RoomTaskStopRequest request) {
        StopTaskRequest stopRequest = StopTaskRequest.builder()
                .cluster(request.clusterArn())
                .task(request.taskArn())
                .reason(stopReason(request))
                .build();
        try {
            ecsClient.stopTask(stopRequest);
            log.info("ECS StopTask submitted room_id={} task_arn={} reason={}",
                    request.roomId(), request.taskArn(), stopRequest.reason());
        } catch (EcsException ex) {
            throw new RoomRuntimeProviderException(
                    "ECS StopTask 호출이 실패했습니다: " + ex.awsErrorDetails().errorMessage(), ex);
        }
    }

    @Override
    public Optional<RoomTaskRuntimeState> describeRoomTask(String clusterArn, String taskArn) {
        DescribeTasksRequest describeRequest = DescribeTasksRequest.builder()
                .cluster(clusterArn)
                .tasks(taskArn)
                .build();
        DescribeTasksResponse response;
        try {
            response = ecsClient.describeTasks(describeRequest);
        } catch (EcsException ex) {
            throw new RoomRuntimeProviderException(
                    "ECS DescribeTasks 호출이 실패했습니다: " + ex.awsErrorDetails().errorMessage(), ex);
        }

        if (response.tasks() == null || response.tasks().isEmpty()) {
            return Optional.empty();
        }
        Task task = response.tasks().get(0);
        String lastStatus = task.lastStatus();
        boolean running = "RUNNING".equals(lastStatus);
        return Optional.of(new RoomTaskRuntimeState(lastStatus, running));
    }

    private NetworkConfiguration networkConfiguration() {
        AssignPublicIp assignPublicIp = properties.assignPublicIpOrDefault()
                ? AssignPublicIp.ENABLED
                : AssignPublicIp.DISABLED;
        return NetworkConfiguration.builder()
                .awsvpcConfiguration(AwsVpcConfiguration.builder()
                        .subnets(properties.subnetIds())
                        .securityGroups(properties.securityGroupIds())
                        .assignPublicIp(assignPublicIp)
                        .build())
                .build();
    }

    private TaskOverride taskOverride(RoomTaskStartRequest request) {
        return TaskOverride.builder()
                .containerOverrides(ContainerOverride.builder()
                        .name(properties.containerName())
                        .environment(buildEnvironment(request))
                        .build())
                .build();
    }

    private List<KeyValuePair> buildEnvironment(RoomTaskStartRequest request) {
        List<KeyValuePair> env = new ArrayList<>();
        env.add(kv(ENV_ROOM_ID, String.valueOf(request.roomId())));
        env.add(kv(ENV_PHOTON_SESSION_NAME, request.photonSessionName()));
        env.add(kv(ENV_MAX_PLAYERS, String.valueOf(request.maxPlayers())));
        env.add(kv(ENV_ROOM_RUNTIME_VERSION, request.roomRuntimeVersion()));
        env.add(kv(ENV_READY_CALLBACK_URL, request.readyCallbackUrl().toString()));
        env.add(kv(ENV_HEARTBEAT_CALLBACK_URL, request.heartbeatCallbackUrl().toString()));

        String sharedSecret = callbackProperties == null ? null : callbackProperties.sharedSecret();
        if (sharedSecret != null && !sharedSecret.isBlank()) {
            env.add(kv(ENV_INTERNAL_CALLBACK_SHARED_SECRET, sharedSecret));
        }

        return env;
    }

    private static KeyValuePair kv(String name, String value) {
        return KeyValuePair.builder().name(name).value(value).build();
    }

    private static String stopReason(RoomTaskStopRequest request) {
        return request.reason() == null || request.reason().isBlank()
                ? "RoomServerManager teardown"
                : request.reason();
    }

    private static String formatFailure(Failure failure) {
        String detail = failure.detail() == null ? "" : failure.detail();
        return failure.reason() + ":" + detail;
    }
}
