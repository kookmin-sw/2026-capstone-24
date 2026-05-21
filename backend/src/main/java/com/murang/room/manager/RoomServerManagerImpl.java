package com.murang.room.manager;

import com.murang.common.exception.ApiException;
import com.murang.room.domain.Room;
import com.murang.room.domain.RoomServerInstance;
import com.murang.room.domain.RoomServerInstanceStatus;
import com.murang.room.repository.RoomRepository;
import com.murang.room.repository.RoomServerInstanceRepository;
import com.murang.room.runtime.ProvisionedRoomTask;
import com.murang.room.runtime.RoomRuntimeProvider;
import com.murang.room.runtime.RoomTaskStartRequest;
import com.murang.room.runtime.RoomTaskStopRequest;
import java.time.Clock;
import java.time.Instant;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class RoomServerManagerImpl implements RoomServerManager {

    private static final Logger log = LoggerFactory.getLogger(RoomServerManagerImpl.class);

    private static final Set<RoomServerInstanceStatus> ADMISSION_OPEN = Set.of(
            RoomServerInstanceStatus.READY, RoomServerInstanceStatus.ACTIVE);

    private static final Set<RoomServerInstanceStatus> ALREADY_DONE = Set.of(
            RoomServerInstanceStatus.TERMINATED, RoomServerInstanceStatus.FAILED);

    private final RoomRepository roomRepository;
    private final RoomServerInstanceRepository instanceRepository;
    private final RoomRuntimeProvider runtimeProvider;
    private final RoomCallbackUrlBuilder callbackUrlBuilder;
    private final Clock clock;

    public RoomServerManagerImpl(RoomRepository roomRepository, RoomServerInstanceRepository instanceRepository,
            RoomRuntimeProvider runtimeProvider, RoomCallbackUrlBuilder callbackUrlBuilder, Clock clock) {
        this.roomRepository = roomRepository; this.instanceRepository = instanceRepository;
        this.runtimeProvider = runtimeProvider; this.callbackUrlBuilder = callbackUrlBuilder; this.clock = clock;
    }

    @Override @Transactional
    public RoomServerSnapshot provision(RoomProvisioningCommand command) {
        Instant now = Instant.now(clock);
        if (roomRepository.findByPhotonSessionName(command.photonSessionName()).isPresent()) {
            throw ApiException.roomNameDuplicate();
        }
        Room room = roomRepository.save(Room.open(command.ownerUserId(), command.photonSessionName(),
                command.maxPlayers(), command.passwordHash(), now));
        RoomServerInstance instance = instanceRepository.save(RoomServerInstance.provision(room.getRoomId(), now));
        RoomTaskStartRequest startRequest = new RoomTaskStartRequest(room.getRoomId(),
                room.getPhotonSessionName(), room.getMaxPlayers(), command.roomRuntimeVersion(),
                callbackUrlBuilder.readyCallbackUrl(room.getRoomId()),
                callbackUrlBuilder.heartbeatCallbackUrl(room.getRoomId()));
        ProvisionedRoomTask provisioned;
        try { provisioned = runtimeProvider.startRoomTask(startRequest); }
        catch (RuntimeException ex) {
            log.error("room runtime provider failed to start task room_id={}", room.getRoomId(), ex);
            instance.markFailed(Instant.now(clock));
            throw ApiException.roomProvisioningFailed(ex.getMessage());
        }
        instance.attachEcsTask(provisioned.clusterArn(), provisioned.taskArn());
        return RoomServerSnapshot.of(room, instance);
    }

    @Override @Transactional
    public void notifyReady(Long roomId, RoomReadySignal signal) {
        requireActiveInstance(roomId).markReady(signal.taskPublicIp(), signal.gamePort(), Instant.now(clock));
    }

    @Override @Transactional
    public void notifyHeartbeat(Long roomId, Instant receivedAt) {
        requireActiveInstance(roomId).recordHeartbeat(receivedAt);
    }

    @Override @Transactional
    public void markActive(Long roomId) { requireActiveInstance(roomId).markActive(); }

    @Override @Transactional
    public void terminate(Long roomId, String reason) {
        Room room = roomRepository.findById(roomId).orElseThrow(ApiException::roomNotFound);
        RoomServerInstance instance = instanceRepository.findByRoomId(roomId).orElseThrow(ApiException::roomNotFound);
        instance.beginTermination();
        tryStopRoomTask(instance, roomId, reason);
        Instant now = Instant.now(clock);
        instance.markTerminated(now); room.close(now);
    }

    @Override @Transactional
    public void markUnhealthyAndTerminate(Long roomId, String reason) {
        Optional<RoomServerInstance> maybeInstance = instanceRepository.findByRoomId(roomId);
        if (maybeInstance.isEmpty()) { log.warn("markUnhealthyAndTerminate: not found room_id={}", roomId); return; }
        RoomServerInstance instance = maybeInstance.get();
        if (ALREADY_DONE.contains(instance.getStatus())) { return; }
        Room room = roomRepository.findById(roomId).orElseThrow(ApiException::roomNotFound);
        instance.markUnhealthy(); instance.beginTermination();
        tryStopRoomTask(instance, roomId, reason);
        Instant now = Instant.now(clock);
        instance.markTerminated(now); room.close(now);
        log.info("markUnhealthyAndTerminate: room_id={} reason={}", roomId, reason);
    }

    @Override @Transactional
    public void markProvisioningTimedOut(Long roomId, String reason) {
        Optional<RoomServerInstance> maybeInstance = instanceRepository.findByRoomId(roomId);
        if (maybeInstance.isEmpty()) { log.warn("markProvisioningTimedOut: not found room_id={}", roomId); return; }
        RoomServerInstance instance = maybeInstance.get();
        if (ALREADY_DONE.contains(instance.getStatus())) { return; }
        Room room = roomRepository.findById(roomId).orElseThrow(ApiException::roomNotFound);
        tryStopRoomTask(instance, roomId, reason);
        Instant now = Instant.now(clock);
        instance.markFailed(now); room.close(now);
        log.info("markProvisioningTimedOut: room_id={} reason={}", roomId, reason);
    }

    @Override @Transactional(readOnly = true)
    public Optional<RoomServerSnapshot> findByRoomId(Long roomId) {
        Optional<Room> room = roomRepository.findById(roomId);
        if (room.isEmpty()) { return Optional.empty(); }
        return instanceRepository.findByRoomId(roomId).map(instance -> RoomServerSnapshot.of(room.get(), instance));
    }

    @Override @Transactional(readOnly = true)
    public List<RoomServerSnapshot> findAdmissionOpen() {
        List<RoomServerInstance> instances = instanceRepository.findAllByStatusIn(List.copyOf(ADMISSION_OPEN));
        if (instances.isEmpty()) { return List.of(); }
        List<Long> roomIds = instances.stream().map(RoomServerInstance::getRoomId).toList();
        Map<Long, Room> roomsById = new HashMap<>();
        roomRepository.findAllById(roomIds).forEach(room -> roomsById.put(room.getRoomId(), room));
        return instances.stream().map(instance -> {
            Room room = roomsById.get(instance.getRoomId());
            return room == null ? null : RoomServerSnapshot.of(room, instance);
        }).filter(snapshot -> snapshot != null && snapshot.closedAt() == null).toList();
    }

    private void tryStopRoomTask(RoomServerInstance instance, Long roomId, String reason) {
        if (instance.getEcsTaskArn() != null && instance.getEcsClusterArn() != null) {
            try { runtimeProvider.stopRoomTask(new RoomTaskStopRequest(
                    roomId, instance.getEcsClusterArn(), instance.getEcsTaskArn(), reason)); }
            catch (RuntimeException ex) {
                log.warn("room runtime provider failed to stop task room_id={}, continuing teardown", roomId, ex);
            }
        }
    }

    private RoomServerInstance requireActiveInstance(Long roomId) {
        return instanceRepository.findByRoomId(roomId).orElseThrow(ApiException::roomNotFound);
    }
}
