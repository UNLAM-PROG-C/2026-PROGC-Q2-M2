package edu.unlam.ratelimiter;

import org.openjdk.jmh.annotations.*;
import org.openjdk.jmh.infra.Blackhole;
import org.openjdk.jmh.infra.ThreadParams;

import java.time.Duration;
import java.util.concurrent.TimeUnit;

@BenchmarkMode(Mode.Throughput)
@OutputTimeUnit(TimeUnit.SECONDS)
@Warmup(iterations = 3, time = 500, timeUnit = TimeUnit.MILLISECONDS)
@Measurement(iterations = 5, time = 500, timeUnit = TimeUnit.MILLISECONDS)
@Fork(1)
public class RateLimiterBenchmark {
    @State(Scope.Benchmark)
    public static class Limiters {
        RateLimiter<String> global;
        RateLimiter<String> perKey;

        @Setup(Level.Iteration)
        public void setup() {
            // A full bucket keeps benchmark memory bounded while retaining synchronization costs.
            global = full(new GlobalLockRateLimiter<>(1, Duration.ofDays(1)));
            perKey = full(new PerKeyRateLimiter<>(1, Duration.ofDays(1)));
        }

        private static RateLimiter<String> full(RateLimiter<String> limiter) {
            limiter.allow("hot-key");
            return limiter;
        }
    }

    @State(Scope.Thread)
    public static class ThreadKey {
        String value;

        @Setup
        public void setup(ThreadParams thread) {
            value = "thread-" + thread.getThreadIndex();
        }
    }

    @Benchmark
    public void globalLockDistinctKeys(Limiters state, ThreadKey key, Blackhole blackhole) {
        blackhole.consume(state.global.allow(key.value));
    }

    @Benchmark
    public void perKeyDistinctKeys(Limiters state, ThreadKey key, Blackhole blackhole) {
        blackhole.consume(state.perKey.allow(key.value));
    }

    @Benchmark
    public void globalLockHotKey(Limiters state, Blackhole blackhole) {
        blackhole.consume(state.global.allow("hot-key"));
    }

    @Benchmark
    public void perKeyHotKey(Limiters state, Blackhole blackhole) {
        blackhole.consume(state.perKey.allow("hot-key"));
    }
}
