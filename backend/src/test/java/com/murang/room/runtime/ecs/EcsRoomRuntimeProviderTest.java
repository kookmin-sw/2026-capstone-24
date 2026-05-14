package com.murang.room.runtime.ecs;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.murang.room.runtime.ProvisionedRoomTask;
import com.murang.room.runtime.RoomRuntimeProviderException;
import com.murang.room.runtime.RoomTaskRuntimeState;
import com.murang.room.runtime.RoomTaskStartRequest;
import com.murang.room.runtime.RoomTaskStopRequest;
import java.net.URI;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import software.amazon.awssdk.awscore.exception.AwsErrorDetails;
import software.amazon.awssdk.services.ecs.EcsClient;
import software.amazon.awssdk.services.ecs.model.AssignPublicIp;
import software.amazon.awssdk.services.ecs.model.DescribeTasksRequest;
import software.amazon.awssdk.services.ecs.model.DescribeTasksResponse;
import software.amazon.awssdk.services.ecs.model.EcsException;
import software.amazon.awssdk.services.ecs.model.Failure;
import software.amazon.awssdk.services.ecs.model.KeyValuePair;
import software.amazon.awssdk.services.ecs.model.LaunchType;
import software.amazon.awssdk.services.ecs.model.RunTaskRequest;
import software.amazon.awssdk.services.ecs.model.RunTaskResponse;
import software.amazon.awssdk.services.ecs.model.StopTaskRequest;
import software.amazon.awssdk.services.ecs.model.Task;

@ExtendWith(MockitoExtension.class)
class EcsRoomRuntimeProviderTest {

    private static final URI READY_URL = URI.create("http://ec2.example/internal/rooms/42/ready");
    private static final URI HEARTBEAT_URL = URI.create("http://ec2.example/internal/rooms/42/heartbeat");

    @Mock
    private EcsClient ecsClient;

    private EcsRoomRuntimeProperties properties;
    private EcsRoomRuntimeProvider provider;

    @BeforeEach
    void setUp() {
        properties = new EcsRoomRuntimeProperties(
                "ap-northeast-2",
                "test-cluster",
                "murang-room-server",
                List.of("subnet-1", "subnet-2"),
                List.of("sg-1"),
                true,
                "room-server"
        );
        provider = new EcsRoomRuntimeProvider(ecsClient, properties);
    }

    @Test
    void startRoomTask_callsRunTaskWithFargateAndOverrideEnv() {
        when(ecsClient.runTask(any(RunTaskRequest.class))).thenReturn(
                RunTaskResponse.builder()
                        .tasks(Task.builder()
                                .clusterArn("arn:cluster")
                                .taskArn("arn:task:abc")
                                .build())
                        .build()
        );

        ProvisionedRoomTask result = provider.startRoomTask(new RoomTaskStartRequest(
                42L,
                "murang-room-a",
                8,
                "v0.1.0",
                READY_URL,
                HEARTBEAT_URL
        ));

        assertThat(result.clusterArn()).isEqualTo("arn:cluster");
        assertThat(result.taskArn()).isEqualTo("arn:task:abc");

        ArgumentCaptor<RunTaskRequest> captor = ArgumentCaptor.forClass(RunTaskRequest.class);
        verify(ecsClient).runTask(captor.capture());
        RunTaskRequest captured = captor.getValue();

        assertThat(captured.cluster()).isEqualTo("test-cluster");
        assertThat(captured.taskDefinition()).isEqualTo("murang-room-server");
        assertThat(captured.launchType()).isEqualTo(LaunchType.FARGATE);
        assertThat(captured.count()).isEqualTo(1);

        assertThat(captured.networkConfiguration().awsvpcConfiguration().subnets())
                .containsExactly("subnet-1", "subnet-2");
        assertThat(captured.networkConfiguration().awsvpcConfiguration().securityGroups())
                .containsExactly("sg-1");
        assertThat(captured.networkConfiguration().awsvpcConfiguration().assignPublicIp())
                .isEqualTo(AssignPublicIp.ENABLED);

        List<KeyValuePair> env = captured.overrides().containerOverrides().get(0).environment();
        Map<String, String> envMap = env.stream()
                .collect(Collectors.toMap(KeyValuePair::name, KeyValuePair::value));

        assertThat(captured.overrides().containerOverrides().get(0).name()).isEqualTo("room-server");
        assertThat(envMap)
                .containsEntry("ROOM_ID", "42")
                .containsEntry("PHOTON_SESSION_NAME", "murang-room-a")
                .containsEntry("MAX_PLAYERS", "8")
                .containsEntry("ROOM_RUNTIME_VERSION", "v0.1.0")
                .containsEntry("ROOM_READY_CALLBACK_URL", READY_URL.toString())
                .containsEntry("ROOM_HEARTBEAT_CALLBACK_URL", HEARTBEAT_URL.toString());
    }

    @Test
    void startRoomTask_assignPublicIpFalse_setsDisabled() {
        properties = new EcsRoomRuntimeProperties(
                "ap-northeast-2", "test-cluster", "murang-room-server",
                List.of("subnet-1"), List.of("sg-1"), false, "room-server"
        );
        provider = new EcsRoomRuntimeProvider(ecsClient, properties);

        when(ecsClient.runTask(any(RunTaskRequest.class))).thenReturn(
                RunTaskResponse.builder()
                        .tasks(Task.builder().clusterArn("a").taskArn("b").build())
                        .build()
        );

        provider.startRoomTask(new RoomTaskStartRequest(1L, "s", 4, "v", READY_URL, HEARTBEAT_URL));

        ArgumentCaptor<RunTaskRequest> captor = ArgumentCaptor.forClass(RunTaskRequest.class);
        verify(ecsClient).runTask(captor.capture());
        assertThat(captor.getValue().networkConfiguration().awsvpcConfiguration().assignPublicIp())
                .isEqualTo(AssignPublicIp.DISABLED);
    }

    @Test
    void startRoomTask_runTaskFailures_throwsProviderException() {
        when(ecsClient.runTask(any(RunTaskRequest.class))).thenReturn(
                RunTaskResponse.builder()
                        .failures(Failure.builder().reason("CAPACITY").detail("insufficient").build())
                        .build()
        );

        assertThatThrownBy(() -> provider.startRoomTask(new RoomTaskStartRequest(
                42L, "session", 4, "v0.1.0", READY_URL, HEARTBEAT_URL)))
                .isInstanceOf(RoomRuntimeProviderException.class)
                .hasMessageContaining("CAPACITY");
    }

    @Test
    void startRoomTask_emptyTasks_throwsProviderException() {
        when(ecsClient.runTask(any(RunTaskRequest.class))).thenReturn(
                RunTaskResponse.builder().tasks(List.of()).build()
        );

        assertThatThrownBy(() -> provider.startRoomTask(new RoomTaskStartRequest(
                42L, "session", 4, "v0.1.0", READY_URL, HEARTBEAT_URL)))
                .isInstanceOf(RoomRuntimeProviderException.class)
                .hasMessageContaining("task 를 반환하지 않았습니다");
    }

    @Test
    void startRoomTask_ecsExceptionWrapped() {
        EcsException ex = (EcsException) EcsException.builder()
                .awsErrorDetails(AwsErrorDetails.builder()
                        .errorMessage("AccessDenied")
                        .errorCode("AccessDeniedException")
                        .build())
                .message("denied")
                .build();
        when(ecsClient.runTask(any(RunTaskRequest.class))).thenThrow(ex);

        assertThatThrownBy(() -> provider.startRoomTask(new RoomTaskStartRequest(
                42L, "session", 4, "v0.1.0", READY_URL, HEARTBEAT_URL)))
                .isInstanceOf(RoomRuntimeProviderException.class)
                .hasMessageContaining("AccessDenied");
    }

    @Test
    void stopRoomTask_callsEcsStopTaskWithReason() {
        provider.stopRoomTask(new RoomTaskStopRequest(
                42L, "arn:cluster", "arn:task:abc", "last user left"));

        ArgumentCaptor<StopTaskRequest> captor = ArgumentCaptor.forClass(StopTaskRequest.class);
        verify(ecsClient).stopTask(captor.capture());
        assertThat(captor.getValue().cluster()).isEqualTo("arn:cluster");
        assertThat(captor.getValue().task()).isEqualTo("arn:task:abc");
        assertThat(captor.getValue().reason()).isEqualTo("last user left");
    }

    @Test
    void stopRoomTask_nullReason_usesDefault() {
        provider.stopRoomTask(new RoomTaskStopRequest(42L, "arn:cluster", "arn:task:abc", null));

        ArgumentCaptor<StopTaskRequest> captor = ArgumentCaptor.forClass(StopTaskRequest.class);
        verify(ecsClient).stopTask(captor.capture());
        assertThat(captor.getValue().reason()).isEqualTo("RoomServerManager teardown");
    }

    @Test
    void describeRoomTask_running_returnsState() {
        when(ecsClient.describeTasks(any(DescribeTasksRequest.class))).thenReturn(
                DescribeTasksResponse.builder()
                        .tasks(Task.builder().lastStatus("RUNNING").build())
                        .build()
        );

        Optional<RoomTaskRuntimeState> state = provider.describeRoomTask("arn:cluster", "arn:task:abc");

        assertThat(state).isPresent();
        assertThat(state.get().lastStatus()).isEqualTo("RUNNING");
        assertThat(state.get().running()).isTrue();
    }

    @Test
    void describeRoomTask_stopped_returnsNotRunning() {
        when(ecsClient.describeTasks(any(DescribeTasksRequest.class))).thenReturn(
                DescribeTasksResponse.builder()
                        .tasks(Task.builder().lastStatus("STOPPED").build())
                        .build()
        );

        Optional<RoomTaskRuntimeState> state = provider.describeRoomTask("arn:cluster", "arn:task:abc");

        assertThat(state).isPresent();
        assertThat(state.get().running()).isFalse();
    }

    @Test
    void describeRoomTask_noTasks_returnsEmpty() {
        when(ecsClient.describeTasks(any(DescribeTasksRequest.class))).thenReturn(
                DescribeTasksResponse.builder().tasks(List.of()).build()
        );

        assertThat(provider.describeRoomTask("arn:cluster", "arn:task:abc")).isEmpty();
    }
}
