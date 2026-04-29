package com.murang.user.service;

import com.murang.common.exception.ApiException;
import com.murang.user.domain.UserProfile;
import java.time.Instant;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicLong;
import org.springframework.stereotype.Service;

@Service
public class InMemoryUserRegistry {

    private final AtomicLong sequence = new AtomicLong(1L);
    private final Map<String, UserProfile> usersByMetaAccountId = new ConcurrentHashMap<>();
    private final Map<String, String> metaAccountIdByNicknameKey = new ConcurrentHashMap<>();

    public synchronized UserProfile registerOrUpdate(String metaAccountId, String nickname) {
        String nicknameKey = nickname.toLowerCase(Locale.ROOT);
        UserProfile existing = usersByMetaAccountId.get(metaAccountId);

        if (existing == null) {
            String claimedBy = metaAccountIdByNicknameKey.putIfAbsent(nicknameKey, metaAccountId);
            if (claimedBy != null && !claimedBy.equals(metaAccountId)) {
                throw ApiException.nicknameDuplicate();
            }

            Instant now = Instant.now();
            UserProfile created = new UserProfile(sequence.getAndIncrement(), metaAccountId, nickname, now, now);
            usersByMetaAccountId.put(metaAccountId, created);
            return created;
        }

        String existingNicknameKey = existing.nickname().toLowerCase(Locale.ROOT);
        if (!existingNicknameKey.equals(nicknameKey)) {
            String claimedBy = metaAccountIdByNicknameKey.putIfAbsent(nicknameKey, metaAccountId);
            if (claimedBy != null && !claimedBy.equals(metaAccountId)) {
                throw ApiException.nicknameDuplicate();
            }
            metaAccountIdByNicknameKey.remove(existingNicknameKey, metaAccountId);
        }

        UserProfile updated = new UserProfile(
                existing.userId(),
                existing.metaAccountId(),
                nickname,
                existing.createdAt(),
                Instant.now()
        );
        usersByMetaAccountId.put(metaAccountId, updated);
        return updated;
    }
}
