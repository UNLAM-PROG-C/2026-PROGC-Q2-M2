package edu.unlam.ratelimiter;

public enum SystemMonotonicClock implements Clock {
    INSTANCE;

    @Override
    public long nanoTime() {
        return System.nanoTime();
    }
}
