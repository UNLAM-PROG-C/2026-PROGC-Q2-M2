package edu.unlam.ratelimiter;

public interface RateLimiter<K> {
    Decision allow(K key);
}
