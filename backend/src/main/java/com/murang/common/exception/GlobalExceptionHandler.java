package com.murang.common.exception;

import com.murang.common.response.ProblemResponseFactory;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.ConstraintViolationException;
import java.util.stream.Collectors;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatusCode;
import org.springframework.http.ProblemDetail;
import org.springframework.http.ResponseEntity;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.validation.BindException;
import org.springframework.validation.FieldError;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.context.request.ServletWebRequest;
import org.springframework.web.context.request.WebRequest;
import org.springframework.web.method.annotation.HandlerMethodValidationException;
import org.springframework.web.servlet.mvc.method.annotation.ResponseEntityExceptionHandler;

@RestControllerAdvice
public class GlobalExceptionHandler extends ResponseEntityExceptionHandler {

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

    @Override
    protected ResponseEntity<Object> handleMethodArgumentNotValid(
            MethodArgumentNotValidException exception,
            HttpHeaders headers,
            HttpStatusCode status,
            WebRequest request
    ) {
        String detail = collectFieldErrors(exception.getBindingResult().getFieldErrors());
        return validationResponse(detail, request);
    }

    @ExceptionHandler(BindException.class)
    public ResponseEntity<Object> handleBindException(
            BindException exception,
            HttpServletRequest request
    ) {
        String detail = collectFieldErrors(exception.getBindingResult().getFieldErrors());
        log.warn("validation-error path={} detail={}", request.getRequestURI(), detail);
        return ResponseEntity.status(ErrorCode.VALIDATION_REQUEST.status())
                .body(problemResponseFactory.create(ErrorCode.VALIDATION_REQUEST, detail, request.getRequestURI()));
    }

    @Override
    protected ResponseEntity<Object> handleHandlerMethodValidationException(
            HandlerMethodValidationException exception,
            HttpHeaders headers,
            HttpStatusCode status,
            WebRequest request
    ) {
        String detail = exception.getParameterValidationResults().stream()
                .flatMap(result -> result.getResolvableErrors().stream())
                .map(error -> error.getDefaultMessage() == null ? ErrorCode.VALIDATION_REQUEST.defaultMessage() : error.getDefaultMessage())
                .collect(Collectors.joining(", "));
        return validationResponse(detail, request);
    }

    @Override
    protected ResponseEntity<Object> handleHttpMessageNotReadable(
            HttpMessageNotReadableException exception,
            HttpHeaders headers,
            HttpStatusCode status,
            WebRequest request
    ) {
        return validationResponse("요청 본문(JSON) 형식이 올바르지 않습니다.", request);
    }

    @ExceptionHandler(ConstraintViolationException.class)
    public ResponseEntity<Object> handleConstraintViolation(
            ConstraintViolationException exception,
            HttpServletRequest request
    ) {
        String detail = exception.getConstraintViolations().stream()
                .map(violation -> violation.getMessage())
                .collect(Collectors.joining(", "));
        log.warn("validation-error path={} detail={}", request.getRequestURI(), detail);
        return ResponseEntity.status(ErrorCode.VALIDATION_REQUEST.status())
                .body(problemResponseFactory.create(ErrorCode.VALIDATION_REQUEST, detail, request.getRequestURI()));
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

    private ResponseEntity<Object> validationResponse(String detail, WebRequest request) {
        String path = requestPath(request);
        log.warn("validation-error path={} detail={}", path, detail);
        return ResponseEntity.status(ErrorCode.VALIDATION_REQUEST.status())
                .body(problemResponseFactory.create(ErrorCode.VALIDATION_REQUEST, detail, path));
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

    private String requestPath(WebRequest request) {
        if (request instanceof ServletWebRequest servletWebRequest) {
            return servletWebRequest.getRequest().getRequestURI();
        }
        return "";
    }
}
