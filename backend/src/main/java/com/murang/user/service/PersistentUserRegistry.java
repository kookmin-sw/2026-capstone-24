package com.murang.user.service;

import com.murang.common.exception.ApiException;
import com.murang.user.domain.UserAccount;
import com.murang.user.domain.UserProfile;
import com.murang.user.repository.UserAccountRepository;
import java.time.Instant;
import java.util.Optional;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class PersistentUserRegistry implements UserRegistry {

    private final UserAccountRepository userAccountRepository;

    public PersistentUserRegistry(UserAccountRepository userAccountRepository) {
        this.userAccountRepository = userAccountRepository;
    }

    @Override
    @Transactional
    public UserProfile registerOrUpdate(String metaAccountId, String nickname) {
        String nicknameKey = UserAccount.nicknameKeyOf(nickname);
        Instant now = Instant.now();

        try {
            UserAccount existing = userAccountRepository.findByMetaAccountId(metaAccountId).orElse(null);
            if (existing == null) {
                ensureNicknameAvailable(nicknameKey, metaAccountId);
                UserAccount created = userAccountRepository.saveAndFlush(UserAccount.create(metaAccountId, nickname, now));
                return created.toProfile();
            }

            if (!existing.getNicknameKey().equals(nicknameKey)) {
                ensureNicknameAvailable(nicknameKey, metaAccountId);
            }

            existing.recordLogin(nickname, now);
            userAccountRepository.flush();
            return existing.toProfile();
        } catch (DataIntegrityViolationException exception) {
            throw ApiException.nicknameDuplicate();
        }
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<UserProfile> findByMetaAccountId(String metaAccountId) {
        return userAccountRepository.findByMetaAccountId(metaAccountId)
                .map(UserAccount::toProfile);
    }

    private void ensureNicknameAvailable(String nicknameKey, String metaAccountId) {
        userAccountRepository.findByNicknameKey(nicknameKey)
                .filter(userAccount -> !userAccount.getMetaAccountId().equals(metaAccountId))
                .ifPresent(userAccount -> {
                    throw ApiException.nicknameDuplicate();
                });
    }
}
