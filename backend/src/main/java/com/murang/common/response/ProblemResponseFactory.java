package com.murang.common.response;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.murang.common.exception.ApiException;
import com.murang.common.exception.ErrorCode;
import com.murang.common.logging.RequestIdFilter;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.net.URI;
import java.time.OffsetDateTime;
import org.slf4j.MDC;
import org.springframework.http.MediaType;
import org.springframework.http.ProblemDetail;
import org.springframework.stereotype.Component;

@Component
public class ProblemResponseFactory {

    private final ObjectMapper objectMapper;

    public ProblemResponseFactory(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    public ProblemDetail create(ErrorCode errorCode, String detail, String path) {
        ProblemDetail problemDetail = ProblemDetail.forStatusAndDetail(errorCode.status(), detail);
        problemDetail.setType(URI.create("about:blank"));
        problemDetail.setTitle(errorCode.name());
        problemDetail.setProperty("code", errorCode.name());
        problemDetail.setProperty("timestamp", OffsetDateTime.now().toString());
        problemDetail.setProperty("path", path);
        String traceId = MDC.get(RequestIdFilter.MDC_KEY);
        if (traceId != null) {
            problemDetail.setProperty("traceId", traceId);
        }
        return problemDetail;
    }

    public void write(HttpServletResponse response, ApiException exception, String path) throws IOException {
        write(response, exception.getErrorCode(), exception.getMessage(), path);
    }

    public void write(HttpServletResponse response, ErrorCode errorCode, String detail, String path) throws IOException {
        if (response.isCommitted()) {
            return;
        }

        ProblemDetail problemDetail = create(errorCode, detail, path);
        response.setStatus(errorCode.status().value());
        response.setContentType(MediaType.APPLICATION_PROBLEM_JSON_VALUE);
        objectMapper.writeValue(response.getOutputStream(), problemDetail);
    }
}
