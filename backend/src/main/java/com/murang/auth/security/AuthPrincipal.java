package com.murang.auth.security;

import java.io.Serial;
import java.io.Serializable;

public record AuthPrincipal(
        long userId,
        String metaAccountId,
        String nickname
) implements Serializable {

    @Serial
    private static final long serialVersionUID = 1L;
}
