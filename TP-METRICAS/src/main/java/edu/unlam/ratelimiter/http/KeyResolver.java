package edu.unlam.ratelimiter.http;

import com.sun.net.httpserver.HttpExchange;

@FunctionalInterface
public interface KeyResolver {
    String resolve(HttpExchange exchange);
}
