package com.murang.auth.filter;

import com.murang.auth.security.AuthPrincipal;
import com.murang.auth.service.JwtTokenService;
import com.murang.common.exception.ApiException;
import com.murang.common.response.ProblemResponseFactory;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.io.Serial;
import java.util.List;
import org.springframework.http.HttpHeaders;
import org.springframework.security.authentication.AbstractAuthenticationToken;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.stereotype.Component;
import org.springframework.util.StringUtils;
import org.springframework.web.filter.OncePerRequestFilter;

@Component
public class JwtAuthenticationFilter extends OncePerRequestFilter {

    private static final String BEARER_PREFIX = "Bearer ";

    private final JwtTokenService jwtTokenService;
    private final ProblemResponseFactory problemResponseFactory;

    public JwtAuthenticationFilter(
            JwtTokenService jwtTokenService,
            ProblemResponseFactory problemResponseFactory
    ) {
        this.jwtTokenService = jwtTokenService;
        this.problemResponseFactory = problemResponseFactory;
    }

    @Override
    protected void doFilterInternal(
            HttpServletRequest request,
            HttpServletResponse response,
            FilterChain filterChain
    ) throws ServletException, IOException {
        String authorization = request.getHeader(HttpHeaders.AUTHORIZATION);
        if (!StringUtils.hasText(authorization)) {
            filterChain.doFilter(request, response);
            return;
        }

        if (!authorization.startsWith(BEARER_PREFIX)) {
            problemResponseFactory.write(response, ApiException.invalidJwt(), request.getRequestURI());
            return;
        }

        String token = authorization.substring(BEARER_PREFIX.length()).trim();
        if (!StringUtils.hasText(token)) {
            problemResponseFactory.write(response, ApiException.invalidJwt(), request.getRequestURI());
            return;
        }

        try {
            AuthPrincipal principal = jwtTokenService.parseAccessToken(token);
            SecurityContextHolder.getContext().setAuthentication(new PreAuthenticatedToken(principal));
            filterChain.doFilter(request, response);
        } catch (ApiException exception) {
            SecurityContextHolder.clearContext();
            problemResponseFactory.write(response, exception, request.getRequestURI());
        }
    }

    private static final class PreAuthenticatedToken extends AbstractAuthenticationToken {

        @Serial
        private static final long serialVersionUID = 1L;

        private final AuthPrincipal principal;

        private PreAuthenticatedToken(AuthPrincipal principal) {
            super(List.of(new SimpleGrantedAuthority("ROLE_USER")));
            this.principal = principal;
            setAuthenticated(true);
        }

        @Override
        public Object getCredentials() {
            return "";
        }

        @Override
        public Object getPrincipal() {
            return principal;
        }
    }
}
