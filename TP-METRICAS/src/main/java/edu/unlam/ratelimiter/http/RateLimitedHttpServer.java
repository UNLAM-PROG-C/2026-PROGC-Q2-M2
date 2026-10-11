package edu.unlam.ratelimiter.http;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;
import edu.unlam.ratelimiter.Decision;
import edu.unlam.ratelimiter.RateLimiter;

import java.io.IOException;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.Objects;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class RateLimitedHttpServer implements AutoCloseable {
    private final HttpServer server;
    private final ExecutorService executor = Executors.newVirtualThreadPerTaskExecutor();
    private final RateLimiter<String> limiter;
    private final KeyResolver keyResolver;
    private final int limit;

    public RateLimitedHttpServer(int port, RateLimiter<String> limiter, KeyResolver keyResolver, int limit)
            throws IOException {
        if (limit <= 0) {
            throw new IllegalArgumentException("limit must be greater than zero");
        }
        this.limiter = Objects.requireNonNull(limiter, "limiter");
        this.keyResolver = Objects.requireNonNull(keyResolver, "keyResolver");
        this.limit = limit;
        server = HttpServer.create(new InetSocketAddress(port), 0);
        server.createContext("/", this::handle);
        server.setExecutor(executor);
    }

    public void start() {
        server.start();
    }

    public int port() {
        return server.getAddress().getPort();
    }

    private void handle(HttpExchange exchange) throws IOException {
        if (!"GET".equals(exchange.getRequestMethod())) {
            send(exchange, 405, "Method Not Allowed\n");
            return;
        }
        final String key;
        try {
            key = keyResolver.resolve(exchange);
        } catch (IllegalArgumentException exception) {
            send(exchange, 400, exception.getMessage() + "\n");
            return;
        }

        Decision decision = limiter.allow(key);
        exchange.getResponseHeaders().set("X-RateLimit-Limit", Integer.toString(limit));
        exchange.getResponseHeaders().set("X-RateLimit-Remaining", Integer.toString(decision.remaining()));
        if (!decision.allowed()) {
            long retrySeconds = Math.max(1, Math.ceilDiv(decision.retryAfter().toNanos(), 1_000_000_000L));
            exchange.getResponseHeaders().set("Retry-After", Long.toString(retrySeconds));
            send(exchange, 429, "Too Many Requests\n");
            return;
        }
        send(exchange, 200, "OK\n");
    }

    private static void send(HttpExchange exchange, int status, String body) throws IOException {
        byte[] bytes = body.getBytes(StandardCharsets.UTF_8);
        exchange.sendResponseHeaders(status, bytes.length);
        try (exchange; var response = exchange.getResponseBody()) {
            response.write(bytes);
        }
    }

    @Override
    public void close() {
        server.stop(0);
        executor.close();
    }
}
