package edu.unlam.ratelimiter;

import java.time.Duration;
import java.util.ArrayDeque;
import java.util.Deque;

final class SlidingWindow {
    private SlidingWindow() {
    }

    static void discardExpired(Deque<Long> timestamps, long now, long windowNanos) {
        while (!timestamps.isEmpty() && elapsed(now, timestamps.peekFirst()) >= windowNanos) {
            timestamps.removeFirst();
        }
    }

    static Decision decide(ArrayDeque<Long> timestamps, long now, LimiterConfiguration configuration) {
        discardExpired(timestamps, now, configuration.windowNanos());
        if (timestamps.size() < configuration.limit()) {
            timestamps.addLast(now);
            return Decision.allowed(configuration.limit() - timestamps.size());
        }
        long remainingNanos = configuration.windowNanos() - elapsed(now, timestamps.peekFirst());
        return Decision.rejected(Duration.ofNanos(Math.max(1, remainingNanos)));
    }

    private static long elapsed(long now, long before) {
        // Subtraction is the documented way to compare System.nanoTime values, including wraparound.
        return now - before;
    }
}
