package com.murang.common.exception;

import com.murang.common.response.ProblemResponseFactory;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.ConstraintViolationException;
import java.util.stream.Collectors;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ProblemDetail;
import org.springframework.validation.BindException;
import org.springframework.validation.FieldError;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.method.annotation.HandlerMethodValidationException;
import org.springframework.http.converter.HttpMessageNotReadableException;

@RestControllerAdvice
public class GlobalExceptionHandler {

    private static final Logger log = LoggerFactory.getLogger(GlobalExceptionHandler.class);

    private final ProblemResponseFactory problemResponseFactory;

    public GlobalExceptionHandler(ProblemResponseFactory problemResponseFactory) {
        this.problemResponseFactory = problemResponseFactory;
    }

    @ExceptionHandler(ApiException.class)
    public ProblemDetail handleApiException(ApiException exception, HttpServletRequest request) {
        log.warn("api-error code={} path={}", exception.getErrorCode().name(), request.getRequestURI());
        return problemResponseFactory.create(
                exception.getErrorCode(),
                exception.getMessage(),
                request.getRequestURI()
        );
    }

    @ExceptionHandler({
            MethodArgumentNotValidException.class,
            BindException.class,
            ConstraintViolationException.class,
            HandlerMethodValidationException.class,
            HttpMessageNotReadableException.class
    })
    public ProblemDetail handleValidation(Exception exception, HttpServletRequest request) {
        String detail = switch (exception) {
            case MethodArgumentNotValidException methodArgumentNotValidException ->
                    collectFieldErrors(methodArgumentNotValidException.getBindingResult().getFieldErrors());
            case BindException bindException ->
                    collectFieldErrors(bindException.getBindingResult().getFieldErrors());
            case ConstraintViolationException constraintViolationException ->
                    constraintViolationException.getConstraintViolations().stream()
                            .map(violation -> violation.getMessage())
                            .collect(Collectors.joining(", "));
            case HandlerMethodValidationException handlerMethodValidationException ->
                    handlerMethodValidationException.getParameterValidationResults().stream()
                            .flatMap(result -> result.getResolvableErrors().stream())
                            .map(error -> error.getDefaultMessage() == null ? ErrorCode.VALIDATION_REQUEST.defaultMessage() : error.getDefaultMessage())
                            .collect(Collectors.joining(", "));
            case HttpMessageNotReadableException ignored -> "요청 본문(JSON) 형식이 올바르지 않습니다.";
            default -> ErrorCode.VALIDATION_REQUEST.defaultMessage();
        };

        log.warn("validation-error path={} detail={}", request.getRequestURI(), detail);
        return problemResponseFactory.create(ErrorCode.VALIDATION_REQUEST, detail, request.getRequestURI());
    }

    @ExceptionHandler(Exception.class)
    public ProblemDetail handleUnexpected(Exception exception, HttpServletRequest request) {
        log.error("unexpected-error path={}", request.getRequestURI(), exception);
        return problemResponseFactory.create(
                ErrorCode.INTERNAL_UNEXPECTED,
                ErrorCode.INTERNAL_UNEXPECTED.defaultMessage(),
                request.getRequestURI()
        );
    }

    private String collectFieldErrors(Iterable<FieldError> fieldErrors) {
        String detail = "";
        String joined = "";
        for (FieldError fieldError : fieldErrors) {
            String message = fieldError.getDefaultMessage();
            String safeMessage = message == null ? ErrorCode.VALIDATION_REQUEST.defaultMessage() : message;
            joined = joined.isEmpty()
                    ? fieldError.getField() + ": " + safeMessage
                    : joined + ", " + fieldError.getField() + ": " + safeMessage;
        }
        if (!joined.isEmpty()) {
            detail = joined;
        }
        return detail.isBlank() ? ErrorCode.VALIDATION_REQUEST.defaultMessage() : detail;
    }
}
