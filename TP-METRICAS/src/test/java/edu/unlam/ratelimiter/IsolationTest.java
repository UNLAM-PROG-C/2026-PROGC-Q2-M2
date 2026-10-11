package edu.unlam.ratelimiter;

import org.junit.jupiter.api.Test;

import java.time.Duration;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.Executors;

import static org.junit.jupiter.api.Assertions.assertEquals;

class IsolationTest {
    @Test
    void eachKeyHasIndependentQuota() throws Exception {
        RateLimiter<String> limiter = new PerKeyRateLimiter<>(5, Duration.ofMinutes(1));
        try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
            List<Callable<Long>> users = java.util.stream.IntStream.range(0, 100)
                    .mapToObj(user -> (Callable<Long>) () -> java.util.stream.IntStream.range(0, 10)
                            .filter(ignored -> limiter.allow("user-" + user).allowed())
                            .count())
                    .toList();
            for (var result : executor.invokeAll(users)) {
                assertEquals(5, result.get());
            }
        }
    }
}
