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

    public static ApiException nicknameDuplicate() {
        return new ApiException(ErrorCode.AUTH_NICKNAME_DUPLICATE);
    }

    public static ApiException invalidNickname(String message) {
        return new ApiException(ErrorCode.VALIDATION_NAME, message);
    }
}
