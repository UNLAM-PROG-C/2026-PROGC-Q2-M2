package edu.unlam.ratelimiter;

import java.time.Duration;
import java.util.Objects;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

/** Periodically delegates cleanup to the limiter's per-key atomic operation. */
public final class InactiveKeyCleaner implements AutoCloseable {
    private final ScheduledExecutorService scheduler;

    public InactiveKeyCleaner(PerKeyRateLimiter<?> limiter, Duration interval) {
        Objects.requireNonNull(limiter, "limiter");
        Objects.requireNonNull(interval, "interval");
        if (interval.isZero() || interval.isNegative()) {
            throw new IllegalArgumentException("interval must be greater than zero");
        }
        scheduler = Executors.newSingleThreadScheduledExecutor(runnable -> {
            Thread thread = new Thread(runnable, "rate-limiter-cleaner");
            thread.setDaemon(true);
            return thread;
        });
        scheduler.scheduleAtFixedRate(limiter::cleanUp, interval.toNanos(), interval.toNanos(), TimeUnit.NANOSECONDS);
    }

    @Override
    public void close() {
        scheduler.shutdownNow();
    }
}
