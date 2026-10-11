package edu.unlam.ratelimiter;

/** A monotonic time source expressed in nanoseconds. */
@FunctionalInterface
public interface Clock {
    long nanoTime();
}
