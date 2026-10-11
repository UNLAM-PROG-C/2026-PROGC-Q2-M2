package edu.unlam.ratelimiter;

import org.junit.jupiter.api.Test;

import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.*;

import static org.junit.jupiter.api.Assertions.assertEquals;

class CorrectnessTest {
    private static final int REPETITIONS = 10_000;
    private static final int CONTENDERS = 8;

    @Test
    void perKeyLimiterNeverExceedsTheLimitAcrossTenThousandRaces() throws Exception {
        try (ExecutorService executor = Executors.newFixedThreadPool(CONTENDERS)) {
            for (int repetition = 0; repetition < REPETITIONS; repetition++) {
                RateLimiter<String> limiter = new PerKeyRateLimiter<>(5, Duration.ofDays(1));
                CyclicBarrier start = new CyclicBarrier(CONTENDERS);
                List<Future<Boolean>> futures = new ArrayList<>(CONTENDERS);
                for (int thread = 0; thread < CONTENDERS; thread++) {
                    futures.add(executor.submit(() -> {
                        start.await();
                        return limiter.allow("user-1").allowed();
                    }));
                }
                int accepted = 0;
                for (Future<Boolean> future : futures) {
                    accepted += future.get(10, TimeUnit.SECONDS) ? 1 : 0;
                }
                assertEquals(5, accepted, "failed at repetition " + repetition);
            }
        }
    }

    @Test
    void globalLockBaselineIsAlsoCorrect() throws Exception {
        assertExactlyFiveAccepted(new GlobalLockRateLimiter<>(5, Duration.ofDays(1)));
    }

    private static void assertExactlyFiveAccepted(RateLimiter<String> limiter) throws Exception {
        try (ExecutorService executor = Executors.newFixedThreadPool(CONTENDERS)) {
            CyclicBarrier start = new CyclicBarrier(CONTENDERS);
            List<Future<Boolean>> futures = new ArrayList<>();
            for (int thread = 0; thread < CONTENDERS; thread++) {
                futures.add(executor.submit(() -> {
                    start.await();
                    return limiter.allow("user-1").allowed();
                }));
            }
            int accepted = 0;
            for (Future<Boolean> future : futures) {
                accepted += future.get() ? 1 : 0;
            }
            assertEquals(5, accepted);
        }
    }
}
