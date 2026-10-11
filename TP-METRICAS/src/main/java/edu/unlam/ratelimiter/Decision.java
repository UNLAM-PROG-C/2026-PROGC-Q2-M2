package edu.unlam.ratelimiter;

import java.time.Duration;
import java.util.Objects;

public record Decision(boolean allowed, Duration retryAfter, int remaining) {
    public Decision {
        Objects.requireNonNull(retryAfter, "retryAfter");
        if (retryAfter.isNegative()) {
            throw new IllegalArgumentException("retryAfter cannot be negative");
        }
        if (remaining < 0) {
            throw new IllegalArgumentException("remaining cannot be negative");
        }
        if (allowed && !retryAfter.isZero()) {
            throw new IllegalArgumentException("an allowed decision cannot have retryAfter");
        }
    }

    public static Decision allowed(int remaining) {
        return new Decision(true, Duration.ZERO, remaining);
    }

    public static Decision rejected(Duration retryAfter) {
        return new Decision(false, retryAfter, 0);
    }
}
