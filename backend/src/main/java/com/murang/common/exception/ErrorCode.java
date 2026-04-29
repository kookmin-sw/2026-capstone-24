package com.murang.common.exception;

import org.springframework.http.HttpStatus;

public enum ErrorCode {
    AUTH_INVALID_META_TOKEN(HttpStatus.UNAUTHORIZED, "Meta ID 토큰 검증에 실패했습니다."),
    AUTH_INVALID_JWT(HttpStatus.UNAUTHORIZED, "인증 토큰이 유효하지 않습니다."),
    AUTH_FORBIDDEN(HttpStatus.FORBIDDEN, "해당 리소스에 접근할 권한이 없습니다."),
    AUTH_NICKNAME_DUPLICATE(HttpStatus.CONFLICT, "이미 사용 중인 닉네임입니다."),
    VALIDATION_REQUEST(HttpStatus.BAD_REQUEST, "요청 값이 유효하지 않습니다."),
    VALIDATION_NAME(HttpStatus.BAD_REQUEST, "닉네임 형식이 유효하지 않습니다."),
    INTERNAL_UNEXPECTED(HttpStatus.INTERNAL_SERVER_ERROR, "예상하지 못한 서버 오류가 발생했습니다.");

    private final HttpStatus status;
    private final String defaultMessage;

    ErrorCode(HttpStatus status, String defaultMessage) {
        this.status = status;
        this.defaultMessage = defaultMessage;
    }

    public HttpStatus status() {
        return status;
    }

    public String defaultMessage() {
        return defaultMessage;
    }
}
