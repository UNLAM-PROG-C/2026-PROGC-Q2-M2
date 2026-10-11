package edu.unlam.ratelimiter.http;

import com.sun.net.httpserver.HttpExchange;

/** Safe default: does not trust client-controlled forwarding headers. */
public final class RemoteAddressKeyResolver implements KeyResolver {
    @Override
    public String resolve(HttpExchange exchange) {
        return exchange.getRemoteAddress().getAddress().getHostAddress();
    }
}
