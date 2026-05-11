package com.murang.auth.service;

import com.murang.auth.config.SecurityProperties;
import com.murang.common.exception.ApiException;
import java.util.Locale;
import java.util.regex.Pattern;
import org.springframework.util.StringUtils;

public class MockMetaIdTokenVerifier implements MetaIdTokenVerifier {

    private static final Pattern ACCOUNT_ID_PATTERN = Pattern.compile("^[A-Za-z0-9._:-]{3,128}$");

    private final SecurityProperties securityProperties;

    public MockMetaIdTokenVerifier(SecurityProperties securityProperties) {
        this.securityProperties = securityProperties;
    }

    @Override
    public MetaIdentity verify(String metaIdToken) {
        String prefix = securityProperties.getMeta().getMockTokenPrefix();
        if (!StringUtils.hasText(prefix) || !metaIdToken.startsWith(prefix)) {
            throw ApiException.invalidMetaToken();
        }

        String accountId = metaIdToken.substring(prefix.length()).trim();
        if (!ACCOUNT_ID_PATTERN.matcher(accountId).matches()) {
            throw ApiException.invalidMetaToken();
        }

        return new MetaIdentity(accountId.toLowerCase(Locale.ROOT));
    }
}
