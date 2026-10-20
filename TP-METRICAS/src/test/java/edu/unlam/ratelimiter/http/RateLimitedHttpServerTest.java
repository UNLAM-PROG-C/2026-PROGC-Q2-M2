package edu.unlam.ratelimiter.http;

import edu.unlam.ratelimiter.PerKeyRateLimiter;
import org.junit.jupiter.api.Test;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;

import static org.junit.jupiter.api.Assertions.assertEquals;

class RateLimitedHttpServerTest {
    @Test
    void returns429WithRetryAfterWhenQuotaIsExhausted() throws Exception {
        int limit = 2;
        var limiter = new PerKeyRateLimiter<String>(limit, Duration.ofSeconds(10));
        try (var server = new RateLimitedHttpServer(0, limiter, exchange -> "test-user", limit);
             var client = HttpClient.newHttpClient()) {
            server.start();
            HttpRequest request = HttpRequest.newBuilder(URI.create("http://localhost:" + server.port() + "/")).build();

            assertEquals(200, client.send(request, HttpResponse.BodyHandlers.discarding()).statusCode());
            HttpResponse<Void> second = client.send(request, HttpResponse.BodyHandlers.discarding());
            assertEquals(200, second.statusCode());
            assertEquals("0", second.headers().firstValue("X-RateLimit-Remaining").orElseThrow());

            HttpResponse<Void> rejected = client.send(request, HttpResponse.BodyHandlers.discarding());
            assertEquals(429, rejected.statusCode());
            assertEquals("10", rejected.headers().firstValue("Retry-After").orElseThrow());
            assertEquals("2", rejected.headers().firstValue("X-RateLimit-Limit").orElseThrow());
        }
    }
}
