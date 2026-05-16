package com.murang.auth.config;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.murang.auth.service.MetaIdTokenVerifier;
import com.murang.auth.service.MockMetaIdTokenVerifier;
import com.murang.auth.service.RealMetaIdTokenVerifier;
import java.time.Duration;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.client.SimpleClientHttpRequestFactory;
import org.springframework.web.client.RestClient;

@Configuration
public class MetaVerifierConfiguration {

    @Bean
    @ConditionalOnProperty(name = "app.security.meta.verifier-mode", havingValue = "mock", matchIfMissing = true)
    public MetaIdTokenVerifier mockMetaIdTokenVerifier(SecurityProperties securityProperties) {
        return new MockMetaIdTokenVerifier(securityProperties);
    }

    @Bean
    @ConditionalOnProperty(name = "app.security.meta.verifier-mode", havingValue = "real")
    public MetaIdTokenVerifier realMetaIdTokenVerifier(
            SecurityProperties securityProperties,
            ObjectMapper objectMapper
    ) {
        SimpleClientHttpRequestFactory factory = new SimpleClientHttpRequestFactory();
        factory.setConnectTimeout((int) Duration.ofSeconds(5).toMillis());
        factory.setReadTimeout((int) Duration.ofSeconds(5).toMillis());

        RestClient restClient = RestClient.builder()
                .requestFactory(factory)
                .build();

        return new RealMetaIdTokenVerifier(securityProperties.getMeta(), restClient, objectMapper);
    }
}
