package edu.unlam.ratelimiter;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledIfSystemProperty;

import java.time.Duration;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.CyclicBarrier;
import java.util.concurrent.Executors;

import static org.junit.jupiter.api.Assertions.assertEquals;

/** This test is opt-in because its expected outcome is a failing Maven build. */
@EnabledIfSystemProperty(named = "runRacyDemo", matches = "true")
class NaiveRaceDemonstrationTest {
    @Test
    void checkThenActExceedsTheLimit() throws Exception {
        int contenders = 20;
        CyclicBarrier afterRead = new CyclicBarrier(contenders);
        RateLimiter<String> broken = new NaiveRacyRateLimiter<>(5, Duration.ofMinutes(1),
                SystemMonotonicClock.INSTANCE, () -> await(afterRead));
        try (var executor = Executors.newFixedThreadPool(contenders)) {
            List<Callable<Boolean>> calls = java.util.stream.IntStream.range(0, contenders)
                    .mapToObj(ignored -> (Callable<Boolean>) () -> broken.allow("user-1").allowed())
                    .toList();
            long accepted = executor.invokeAll(calls).stream().filter(future -> get(future)).count();
            assertEquals(5, accepted, "deliberately fails: all contenders read the same stale history");
        }
    }

    private static void await(CyclicBarrier barrier) {
        try {
            barrier.await();
        } catch (Exception exception) {
            throw new RuntimeException(exception);
        }
    }

    private static boolean get(java.util.concurrent.Future<Boolean> future) {
        try {
            return future.get();
        } catch (Exception exception) {
            throw new RuntimeException(exception);
        }
    }
}
