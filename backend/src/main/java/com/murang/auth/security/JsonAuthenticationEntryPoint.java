package com.murang.auth.security;

import com.murang.common.exception.ApiException;
import com.murang.common.response.ProblemResponseFactory;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import org.springframework.security.core.AuthenticationException;
import org.springframework.security.web.AuthenticationEntryPoint;
import org.springframework.stereotype.Component;

@Component
public class JsonAuthenticationEntryPoint implements AuthenticationEntryPoint {

    private final ProblemResponseFactory problemResponseFactory;

    public JsonAuthenticationEntryPoint(ProblemResponseFactory problemResponseFactory) {
        this.problemResponseFactory = problemResponseFactory;
    }

    @Override
    public void commence(
            HttpServletRequest request,
            HttpServletResponse response,
            AuthenticationException authException
    ) throws IOException, ServletException {
        problemResponseFactory.write(response, ApiException.invalidJwt(), request.getRequestURI());
    }
}
