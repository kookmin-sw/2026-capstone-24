package com.murang.user.repository;

import com.murang.user.domain.UserAccount;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserAccountRepository extends JpaRepository<UserAccount, Long> {

    Optional<UserAccount> findByMetaAccountId(String metaAccountId);

    Optional<UserAccount> findByNicknameKey(String nicknameKey);
}
