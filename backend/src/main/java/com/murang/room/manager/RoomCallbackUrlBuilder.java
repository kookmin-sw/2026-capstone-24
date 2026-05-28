package com.murang.room.manager;

import com.murang.room.config.RoomInternalCallbackProperties;
import java.net.URI;
import org.springframework.stereotype.Component;

@Component
public class RoomCallbackUrlBuilder {

    private final RoomInternalCallbackProperties properties;

    public RoomCallbackUrlBuilder(RoomInternalCallbackProperties properties) {
        this.properties = properties;
    }

    public URI readyCallbackUrl(Long roomId) {
        return resolve("/internal/rooms/" + roomId + "/ready");
    }

    public URI heartbeatCallbackUrl(Long roomId) {
        return resolve("/internal/rooms/" + roomId + "/heartbeat");
    }

    public URI terminateCallbackUrl(Long roomId) {
        return resolve("/internal/rooms/" + roomId + "/terminate");
    }

    private URI resolve(String path) {
        String base = properties.baseUrl();
        if (base == null || base.isBlank()) {
            throw new IllegalStateException(
                    "murang.room.internal-callback.base-url 가 설정되어 있지 않습니다.");
        }
        String trimmed = base.endsWith("/") ? base.substring(0, base.length() - 1) : base;
        return URI.create(trimmed + path);
    }
}
