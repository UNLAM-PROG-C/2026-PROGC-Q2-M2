package edu.unlam.ratelimiter;

import org.junit.jupiter.api.Test;

import java.time.Duration;

import static org.junit.jupiter.api.Assertions.*;

class WindowExpiryTest {
    @Test
    void expiresAtTheExactWindowBoundaryAndReportsRetryAfter() {
        MutableClock clock = new MutableClock();
        RateLimiter<String> limiter = new PerKeyRateLimiter<>(5, Duration.ofSeconds(10), clock);

        for (int remaining = 4; remaining >= 0; remaining--) {
            Decision decision = limiter.allow("user-1");
            assertTrue(decision.allowed());
            assertEquals(remaining, decision.remaining());
        }

        Decision rejected = limiter.allow("user-1");
        assertFalse(rejected.allowed());
        assertEquals(Duration.ofSeconds(10), rejected.retryAfter());
        assertEquals(0, rejected.remaining());

        clock.advance(Duration.ofSeconds(9));
        assertEquals(Duration.ofSeconds(1), limiter.allow("user-1").retryAfter());

        clock.advance(Duration.ofSeconds(1));
        Decision atBoundary = limiter.allow("user-1");
        assertTrue(atBoundary.allowed());
        assertEquals(4, atBoundary.remaining(), "rejections must not consume quota");
    }

    @Test
    void validatesConfiguration() {
        assertThrows(IllegalArgumentException.class,
                () -> new PerKeyRateLimiter<>(0, Duration.ofSeconds(1)));
        assertThrows(IllegalArgumentException.class,
                () -> new PerKeyRateLimiter<>(1, Duration.ZERO));
    }
}
