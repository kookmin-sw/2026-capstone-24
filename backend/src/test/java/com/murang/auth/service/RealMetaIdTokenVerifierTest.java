package com.murang.auth.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.springframework.test.web.client.match.MockRestRequestMatchers.requestTo;
import static org.springframework.test.web.client.response.MockRestResponseCreators.withServerError;
import static org.springframework.test.web.client.response.MockRestResponseCreators.withStatus;
import static org.springframework.test.web.client.response.MockRestResponseCreators.withSuccess;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.murang.auth.config.SecurityProperties;
import com.murang.common.exception.ApiException;
import com.murang.common.exception.ErrorCode;
import java.net.SocketTimeoutException;
import java.util.Base64;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.test.web.client.MockRestServiceServer;
import org.springframework.web.client.ResourceAccessException;
import org.springframework.web.client.RestClient;

class RealMetaIdTokenVerifierTest {

    private static final String TEST_APP_ID = "test-app-id";
    private static final String TEST_APP_SECRET = "test-app-secret";
    private static final String TEST_USER_ID = "user-123";
    private static final String TEST_USER_PROOF = "proof-abc";

    private MockRestServiceServer mockServer;
    private RealMetaIdTokenVerifier verifier;

    @BeforeEach
    void setUp() {
        SecurityProperties securityProperties = new SecurityProperties();
        securityProperties.getMeta().setAppId(TEST_APP_ID);
        securityProperties.getMeta().setAppSecret(TEST_APP_SECRET);
        securityProperties.getMeta().setGraphBaseUrl("https://graph.oculus.com");

        RestClient.Builder builder = RestClient.builder();
        mockServer = MockRestServiceServer.bindTo(builder).build();
        RestClient restClient = builder.build();

        verifier = new RealMetaIdTokenVerifier(
                securityProperties.getMeta(),
                restClient,
                new ObjectMapper()
        );
    }

    private String buildToken(String userId, String userProof) throws Exception {
        String json = String.format("{\"userId\":\"%s\",\"userProof\":\"%s\"}", userId, userProof);
        return "meta-user-proof:" + Base64.getUrlEncoder().withoutPadding().encodeToString(json.getBytes());
    }

    @Test
    void verify_success_returnsMetaIdentity() throws Exception {
        mockServer.expect(requestTo(org.hamcrest.Matchers.containsString("/user_proof_validate")))
                .andRespond(withSuccess("{\"is_valid\":true}", MediaType.APPLICATION_JSON));

        String token = buildToken(TEST_USER_ID, TEST_USER_PROOF);
        MetaIdentity result = verifier.verify(token);

        assertThat(result.metaAccountId()).isEqualTo(TEST_USER_ID);
        mockServer.verify();
    }

    @Test
    void verify_missingPrefix_throwsInvalidMetaToken() {
        assertThatThrownBy(() -> verifier.verify("no-prefix-token"))
                .isInstanceOf(ApiException.class)
                .satisfies(ex -> assertThat(((ApiException) ex).getErrorCode())
                        .isEqualTo(ErrorCode.AUTH_INVALID_META_TOKEN));
    }

    @Test
    void verify_brokenBase64_throwsInvalidMetaToken() {
        assertThatThrownBy(() -> verifier.verify("meta-user-proof:!!!not-base64!!!"))
                .isInstanceOf(ApiException.class)
                .satisfies(ex -> assertThat(((ApiException) ex).getErrorCode())
                        .isEqualTo(ErrorCode.AUTH_INVALID_META_TOKEN));
    }

    @Test
    void verify_isValidFalse_throwsInvalidMetaToken() throws Exception {
        mockServer.expect(requestTo(org.hamcrest.Matchers.containsString("/user_proof_validate")))
                .andRespond(withSuccess("{\"is_valid\":false}", MediaType.APPLICATION_JSON));

        String token = buildToken(TEST_USER_ID, TEST_USER_PROOF);
        assertThatThrownBy(() -> verifier.verify(token))
                .isInstanceOf(ApiException.class)
                .satisfies(ex -> assertThat(((ApiException) ex).getErrorCode())
                        .isEqualTo(ErrorCode.AUTH_INVALID_META_TOKEN));

        mockServer.verify();
    }

    @Test
    void verify_graphApi4xx_throwsInvalidMetaToken() throws Exception {
        mockServer.expect(requestTo(org.hamcrest.Matchers.containsString("/user_proof_validate")))
                .andRespond(withStatus(HttpStatus.BAD_REQUEST));

        String token = buildToken(TEST_USER_ID, TEST_USER_PROOF);
        assertThatThrownBy(() -> verifier.verify(token))
                .isInstanceOf(ApiException.class)
                .satisfies(ex -> assertThat(((ApiException) ex).getErrorCode())
                        .isEqualTo(ErrorCode.AUTH_INVALID_META_TOKEN));

        mockServer.verify();
    }

    @Test
    void verify_graphApi5xx_throwsMetaVerifierUnavailable() throws Exception {
        mockServer.expect(requestTo(org.hamcrest.Matchers.containsString("/user_proof_validate")))
                .andRespond(withServerError());

        String token = buildToken(TEST_USER_ID, TEST_USER_PROOF);
        assertThatThrownBy(() -> verifier.verify(token))
                .isInstanceOf(ApiException.class)
                .satisfies(ex -> assertThat(((ApiException) ex).getErrorCode())
                        .isEqualTo(ErrorCode.META_VERIFIER_UNAVAILABLE));

        mockServer.verify();
    }

    @Test
    void verify_timeout_throwsMetaVerifierUnavailable() throws Exception {
        mockServer.expect(requestTo(org.hamcrest.Matchers.containsString("/user_proof_validate")))
                .andRespond(request -> {
                    throw new ResourceAccessException("timeout", new SocketTimeoutException("Read timed out"));
                });

        String token = buildToken(TEST_USER_ID, TEST_USER_PROOF);
        assertThatThrownBy(() -> verifier.verify(token))
                .isInstanceOf(ApiException.class)
                .satisfies(ex -> assertThat(((ApiException) ex).getErrorCode())
                        .isEqualTo(ErrorCode.META_VERIFIER_UNAVAILABLE));

        mockServer.verify();
    }
}
