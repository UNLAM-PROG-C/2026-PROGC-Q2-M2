package edu.unlam.ratelimiter;

import org.junit.jupiter.api.Test;

import java.time.Duration;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicInteger;

import static org.junit.jupiter.api.Assertions.*;

class CleanupTest {
    @Test
    void removesOnlyExpiredHistories() {
        MutableClock clock = new MutableClock();
        PerKeyRateLimiter<String> limiter = new PerKeyRateLimiter<>(5, Duration.ofSeconds(10), clock);
        limiter.allow("old");
        clock.advance(Duration.ofSeconds(9));
        limiter.allow("recent");
        clock.advance(Duration.ofSeconds(1));

        assertEquals(1, limiter.cleanUp());
        assertEquals(1, limiter.trackedKeyCount());
        assertTrue(limiter.allow("old").allowed());
        assertEquals(2, limiter.trackedKeyCount());
    }

    @Test
    void concurrentCleanupCannotCreateAnOrphanBucket() throws Exception {
        PerKeyRateLimiter<String> limiter = new PerKeyRateLimiter<>(5, Duration.ofDays(1));
        AtomicInteger accepted = new AtomicInteger();
        try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
            for (int index = 0; index < 1_000; index++) {
                executor.submit(() -> {
                    if (limiter.allow("same-user").allowed()) {
                        accepted.incrementAndGet();
                    }
                });
                executor.submit(limiter::cleanUp);
            }
        }
        assertEquals(5, accepted.get());
    }
}
