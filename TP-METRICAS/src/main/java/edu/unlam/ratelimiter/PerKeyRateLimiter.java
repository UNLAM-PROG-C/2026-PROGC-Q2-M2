package edu.unlam.ratelimiter;

import java.time.Duration;
import java.util.ArrayDeque;
import java.util.Objects;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;

/** Exact sliding-window limiter. ConcurrentHashMap serializes the short compute only for the affected key. */
public final class PerKeyRateLimiter<K> implements RateLimiter<K> {
    public static final int DEFAULT_LIMIT = 5;
    public static final Duration DEFAULT_WINDOW = Duration.ofSeconds(10);

    private final LimiterConfiguration configuration;
    private final ConcurrentHashMap<K, ArrayDeque<Long>> histories = new ConcurrentHashMap<>();

    public PerKeyRateLimiter() {
        this(DEFAULT_LIMIT, DEFAULT_WINDOW);
    }

    public PerKeyRateLimiter(int limit, Duration window) {
        this(limit, window, SystemMonotonicClock.INSTANCE);
    }

    public PerKeyRateLimiter(int limit, Duration window, Clock clock) {
        configuration = new LimiterConfiguration(limit, window, clock);
    }

    @Override
    public Decision allow(K key) {
        Objects.requireNonNull(key, "key");
        long now = configuration.clock().nanoTime();
        AtomicReference<Decision> result = new AtomicReference<>();
        histories.compute(key, (ignored, existing) -> {
            ArrayDeque<Long> timestamps = existing == null ? new ArrayDeque<>() : existing;
            result.set(SlidingWindow.decide(timestamps, now, configuration));
            return timestamps;
        });
        return result.get();
    }

    /**
     * Removes expired, empty histories. computeIfPresent coordinates cleanup with allow for the same key,
     * so no thread can update an orphaned bucket.
     */
    public int cleanUp() {
        long now = configuration.clock().nanoTime();
        AtomicInteger removed = new AtomicInteger();
        histories.forEach((key, ignored) -> histories.computeIfPresent(key, (presentKey, timestamps) -> {
            SlidingWindow.discardExpired(timestamps, now, configuration.windowNanos());
            if (timestamps.isEmpty()) {
                removed.incrementAndGet();
                return null;
            }
            return timestamps;
        }));
        return removed.get();
    }

    public int trackedKeyCount() {
        return histories.size();
    }
}
