package edu.unlam.ratelimiter;

import java.time.Duration;
import java.util.concurrent.atomic.AtomicLong;

final class MutableClock implements Clock {
    private final AtomicLong now = new AtomicLong();

    @Override
    public long nanoTime() {
        return now.get();
    }

    void advance(Duration duration) {
        now.addAndGet(duration.toNanos());
    }
}
