package com.murang.auth.config;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import java.time.Duration;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.validation.annotation.Validated;

@Validated
@ConfigurationProperties(prefix = "app.security")
public class SecurityProperties {

    @Valid
    private final Jwt jwt = new Jwt();

    @Valid
    private final Meta meta = new Meta();

    public Jwt getJwt() {
        return jwt;
    }

    public Meta getMeta() {
        return meta;
    }

    public static final class Jwt {

        @NotBlank
        private String issuer = "murang-backend";

        @NotNull
        private Duration accessTokenTtl = Duration.ofMinutes(15);

        @NotNull
        private Duration refreshTokenTtl = Duration.ofDays(7);

        private String secret;

        private boolean allowEphemeralSecret = true;

        public String getIssuer() {
            return issuer;
        }

        public void setIssuer(String issuer) {
            this.issuer = issuer;
        }

        public Duration getAccessTokenTtl() {
            return accessTokenTtl;
        }

        public void setAccessTokenTtl(Duration accessTokenTtl) {
            this.accessTokenTtl = accessTokenTtl;
        }

        public Duration getRefreshTokenTtl() {
            return refreshTokenTtl;
        }

        public void setRefreshTokenTtl(Duration refreshTokenTtl) {
            this.refreshTokenTtl = refreshTokenTtl;
        }

        public String getSecret() {
            return secret;
        }

        public void setSecret(String secret) {
            this.secret = secret;
        }

        public boolean isAllowEphemeralSecret() {
            return allowEphemeralSecret;
        }

        public void setAllowEphemeralSecret(boolean allowEphemeralSecret) {
            this.allowEphemeralSecret = allowEphemeralSecret;
        }
    }

    public static final class Meta {

        @NotBlank
        private String verifierMode = "mock";

        @NotBlank
        private String mockTokenPrefix = "mock-meta:";

        public String getVerifierMode() {
            return verifierMode;
        }

        public void setVerifierMode(String verifierMode) {
            this.verifierMode = verifierMode;
        }

        public String getMockTokenPrefix() {
            return mockTokenPrefix;
        }

        public void setMockTokenPrefix(String mockTokenPrefix) {
            this.mockTokenPrefix = mockTokenPrefix;
        }
    }
}
