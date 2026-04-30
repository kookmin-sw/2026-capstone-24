package com.murang.user.service;

import com.murang.user.domain.UserProfile;
import java.util.Optional;

public interface UserRegistry {

    UserProfile registerOrUpdate(String metaAccountId, String nickname);

    Optional<UserProfile> findByMetaAccountId(String metaAccountId);
}
