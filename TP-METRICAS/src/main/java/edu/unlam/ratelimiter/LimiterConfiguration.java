package edu.unlam.ratelimiter;

import java.time.Duration;
import java.util.Objects;

record LimiterConfiguration(int limit, Duration window, long windowNanos, Clock clock) {
    LimiterConfiguration(int limit, Duration window, Clock clock) {
        this(limit, validateWindow(window), validateWindow(window).toNanos(), Objects.requireNonNull(clock, "clock"));
        if (limit <= 0) {
            throw new IllegalArgumentException("limit must be greater than zero");
        }
    }

    private static Duration validateWindow(Duration window) {
        Objects.requireNonNull(window, "window");
        if (window.isZero() || window.isNegative()) {
            throw new IllegalArgumentException("window must be greater than zero");
        }
        try {
            window.toNanos();
        } catch (ArithmeticException exception) {
            throw new IllegalArgumentException("window is too large", exception);
        }
        return window;
    }
}
