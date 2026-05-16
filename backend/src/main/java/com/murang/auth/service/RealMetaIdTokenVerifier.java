package com.murang.auth.service;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;
import com.murang.auth.config.SecurityProperties;
import com.murang.auth.dto.MetaUserProofPayload;
import com.murang.common.exception.ApiException;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.util.Base64;
import org.springframework.web.client.HttpClientErrorException;
import org.springframework.web.client.HttpServerErrorException;
import org.springframework.web.client.ResourceAccessException;
import org.springframework.web.client.RestClient;

public class RealMetaIdTokenVerifier implements MetaIdTokenVerifier {

    private static final String PREFIX = "meta-user-proof:";

    private final SecurityProperties.Meta metaProperties;
    private final RestClient restClient;
    private final ObjectMapper objectMapper;

    public RealMetaIdTokenVerifier(
            SecurityProperties.Meta metaProperties,
            RestClient restClient,
            ObjectMapper objectMapper
    ) {
        this.metaProperties = metaProperties;
        this.restClient = restClient;
        this.objectMapper = objectMapper;
    }

    @Override
    public MetaIdentity verify(String metaIdToken) {
        if (metaIdToken == null || !metaIdToken.startsWith(PREFIX)) {
            throw ApiException.invalidMetaToken();
        }

        String encoded = metaIdToken.substring(PREFIX.length());
        MetaUserProofPayload payload = decodePayload(encoded);

        callGraphApi(payload);

        return new MetaIdentity(payload.userId());
    }

    private MetaUserProofPayload decodePayload(String encoded) {
        try {
            byte[] bytes = Base64.getUrlDecoder().decode(encoded);
            return objectMapper.readValue(bytes, MetaUserProofPayload.class);
        } catch (Exception e) {
            throw ApiException.invalidMetaToken();
        }
    }

    private void callGraphApi(MetaUserProofPayload payload) {
        String accessToken = metaProperties.getAppId() + "|" + metaProperties.getAppSecret();
        String url = metaProperties.getGraphBaseUrl()
                + "/user_proof_validate?access_token=" + accessToken
                + "&user_id=" + payload.userId()
                + "&nonce=" + payload.userProof();

        try {
            GraphApiResponse response = restClient.get()
                    .uri(url)
                    .retrieve()
                    .body(GraphApiResponse.class);

            if (response == null || !Boolean.TRUE.equals(response.isValid())) {
                throw ApiException.invalidMetaToken();
            }
        } catch (HttpClientErrorException e) {
            throw ApiException.invalidMetaToken();
        } catch (HttpServerErrorException e) {
            throw ApiException.metaVerifierUnavailable();
        } catch (ResourceAccessException e) {
            throw ApiException.metaVerifierUnavailable();
        }
    }

    @JsonIgnoreProperties(ignoreUnknown = true)
    public static final class GraphApiResponse {

        @JsonProperty("is_valid")
        private Boolean isValid;

        public Boolean isValid() {
            return isValid;
        }

        public void setIsValid(Boolean isValid) {
            this.isValid = isValid;
        }
    }
}
