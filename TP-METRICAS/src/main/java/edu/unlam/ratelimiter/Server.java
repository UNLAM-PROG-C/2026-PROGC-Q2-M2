package edu.unlam.ratelimiter;

import edu.unlam.ratelimiter.http.RateLimitedHttpServer;
import edu.unlam.ratelimiter.http.RemoteAddressKeyResolver;

import java.time.Duration;
import java.util.concurrent.CountDownLatch;

public final class Server {
    private Server() {
    }

    public static void main(String[] args) throws Exception {
        int port = args.length == 0 ? 8080 : Integer.parseInt(args[0]);
        int limit = PerKeyRateLimiter.DEFAULT_LIMIT;
        Duration window = PerKeyRateLimiter.DEFAULT_WINDOW;
        PerKeyRateLimiter<String> limiter = new PerKeyRateLimiter<>(limit, window);
        var server = new RateLimitedHttpServer(port, limiter, new RemoteAddressKeyResolver(), limit);
        var cleaner = new InactiveKeyCleaner(limiter, window);
        Runtime.getRuntime().addShutdownHook(new Thread(() -> {
            cleaner.close();
            server.close();
        }));
        server.start();
        System.out.printf("Rate limiter listening at http://localhost:%d/%n", server.port());
        new CountDownLatch(1).await();
    }
}
