package com.murang.auth.security;

import com.murang.common.exception.ApiException;
import com.murang.common.response.ProblemResponseFactory;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.web.access.AccessDeniedHandler;
import org.springframework.stereotype.Component;

@Component
public class JsonAccessDeniedHandler implements AccessDeniedHandler {

    private final ProblemResponseFactory problemResponseFactory;

    public JsonAccessDeniedHandler(ProblemResponseFactory problemResponseFactory) {
        this.problemResponseFactory = problemResponseFactory;
    }

    @Override
    public void handle(
            HttpServletRequest request,
            HttpServletResponse response,
            AccessDeniedException accessDeniedException
    ) throws IOException, ServletException {
        problemResponseFactory.write(response, ApiException.forbidden(), request.getRequestURI());
    }
}
