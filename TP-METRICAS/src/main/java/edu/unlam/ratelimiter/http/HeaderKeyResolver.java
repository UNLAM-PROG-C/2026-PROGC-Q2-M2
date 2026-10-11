package edu.unlam.ratelimiter.http;

import com.sun.net.httpserver.HttpExchange;

import java.util.Objects;

/** Use only when a trusted proxy strips and rewrites this header. */
public final class HeaderKeyResolver implements KeyResolver {
    private final String headerName;

    public HeaderKeyResolver(String headerName) {
        this.headerName = Objects.requireNonNull(headerName, "headerName");
    }

    @Override
    public String resolve(HttpExchange exchange) {
        String value = exchange.getRequestHeaders().getFirst(headerName);
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException("missing required header: " + headerName);
        }
        return value.trim();
    }
}
