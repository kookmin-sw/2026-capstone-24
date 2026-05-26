package com.murang.common.exception;

import java.io.Serial;

public class ApiException extends RuntimeException {

    @Serial
    private static final long serialVersionUID = 1L;

    private final ErrorCode errorCode;

    public ApiException(ErrorCode errorCode) {
        this(errorCode, errorCode.defaultMessage());
    }

    public ApiException(ErrorCode errorCode, String message) {
        super(message);
        this.errorCode = errorCode;
    }

    public ErrorCode getErrorCode() {
        return errorCode;
    }

    public static ApiException invalidMetaToken() {
        return new ApiException(ErrorCode.AUTH_INVALID_META_TOKEN);
    }

    public static ApiException invalidJwt() {
        return new ApiException(ErrorCode.AUTH_INVALID_JWT);
    }

    public static ApiException forbidden() {
        return new ApiException(ErrorCode.AUTH_FORBIDDEN);
    }

    public static ApiException nicknameRequired() {
        return new ApiException(ErrorCode.AUTH_NICKNAME_REQUIRED);
    }

    public static ApiException nicknameDuplicate() {
        return new ApiException(ErrorCode.AUTH_NICKNAME_DUPLICATE);
    }

    public static ApiException invalidNickname(String message) {
        return new ApiException(ErrorCode.VALIDATION_NAME, message);
    }

    public static ApiException metaVerifierUnavailable() {
        return new ApiException(ErrorCode.META_VERIFIER_UNAVAILABLE);
    }

    public static ApiException roomNotFound() {
        return new ApiException(ErrorCode.ROOM_NOT_FOUND);
    }

    public static ApiException roomInternalForbidden() {
        return new ApiException(ErrorCode.ROOM_INTERNAL_FORBIDDEN);
    }

    public static ApiException roomProvisioningFailed(String message) {
        return new ApiException(ErrorCode.ROOM_PROVISIONING_FAILED, message);
    }

    public static ApiException roomNameDuplicate() {
        return new ApiException(ErrorCode.ROOM_NAME_DUPLICATE);
    }
}
