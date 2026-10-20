package edu.unlam.ratelimiter;

import java.time.Duration;
import java.util.ArrayDeque;
import java.util.HashMap;
import java.util.Map;
import java.util.Objects;
import java.util.concurrent.locks.ReentrantLock;

/** Correct baseline that intentionally serializes every key behind one lock. */
public final class GlobalLockRateLimiter<K> implements RateLimiter<K> {
    private final LimiterConfiguration configuration;
    private final Map<K, ArrayDeque<Long>> histories = new HashMap<>();
    private final ReentrantLock lock = new ReentrantLock();

    public GlobalLockRateLimiter(int limit, Duration window) {
        this(limit, window, SystemMonotonicClock.INSTANCE);
    }

    public GlobalLockRateLimiter(int limit, Duration window, Clock clock) {
        configuration = new LimiterConfiguration(limit, window, clock);
    }

    @Override
    public Decision allow(K key) {
        Objects.requireNonNull(key, "key");
        long now = configuration.clock().nanoTime();
        lock.lock();
        try {
            return SlidingWindow.decide(histories.computeIfAbsent(key, ignored -> new ArrayDeque<>()), now, configuration);
        } finally {
            lock.unlock();
        }
    }
}
