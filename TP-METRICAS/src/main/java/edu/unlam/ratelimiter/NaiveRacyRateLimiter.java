package edu.unlam.ratelimiter;

import java.time.Duration;
import java.util.ArrayDeque;
import java.util.List;
import java.util.Objects;
import java.util.concurrent.ConcurrentHashMap;

/** Deliberately broken check-then-act implementation, retained only to demonstrate the race. */
public final class NaiveRacyRateLimiter<K> implements RateLimiter<K> {
    private final LimiterConfiguration configuration;
    private final ConcurrentHashMap<K, List<Long>> histories = new ConcurrentHashMap<>();
    private final Runnable afterReadHook;

    public NaiveRacyRateLimiter(int limit, Duration window) {
        this(limit, window, SystemMonotonicClock.INSTANCE, () -> { });
    }

    public NaiveRacyRateLimiter(int limit, Duration window, Clock clock, Runnable afterReadHook) {
        configuration = new LimiterConfiguration(limit, window, clock);
        this.afterReadHook = Objects.requireNonNull(afterReadHook, "afterReadHook");
    }

    @Override
    public Decision allow(K key) {
        Objects.requireNonNull(key, "key");
        long now = configuration.clock().nanoTime();
        ArrayDeque<Long> local = new ArrayDeque<>(histories.getOrDefault(key, List.of()));
        SlidingWindow.discardExpired(local, now, configuration.windowNanos());
        afterReadHook.run(); // Test hook makes the unsafe interleaving reproducible.
        if (local.size() >= configuration.limit()) {
            long remaining = configuration.windowNanos() - (now - local.peekFirst());
            return Decision.rejected(Duration.ofNanos(Math.max(1, remaining)));
        }
        local.addLast(now);
        histories.put(key, List.copyOf(local)); // Another thread may overwrite an update read above.
        return Decision.allowed(configuration.limit() - local.size());
    }
}
