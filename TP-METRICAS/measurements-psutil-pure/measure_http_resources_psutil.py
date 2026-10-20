"""Generate HTTP load in Python and measure the Java process with psutil.

The script runs two actions for the configured duration:
1. 32 concurrent HTTP workers (configurable with --concurrency).
2. One sequential HTTP worker.

CPU follows psutil's process convention: 100% means one fully used logical CPU.
Memory is the Java process resident set size (RSS) in MiB.
"""

from __future__ import annotations

import argparse
import csv
import http.client
import statistics
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from dataclasses import asdict, dataclass
from pathlib import Path
from urllib.parse import urlsplit

import psutil


@dataclass
class RunResult:
    action: str
    repetition: int
    concurrency: int
    requests: int
    errors: int
    average_cpu_percent_one_core: float
    peak_rss_mb: float


def http_worker(url: str, deadline: float, start: threading.Event) -> tuple[int, int]:
    """Send requests sequentially from one worker until the common deadline."""
    parsed = urlsplit(url)
    connection_type = (
        http.client.HTTPSConnection if parsed.scheme == "https" else http.client.HTTPConnection
    )
    host = parsed.hostname or "localhost"
    port = parsed.port
    path = parsed.path or "/"
    if parsed.query:
        path += f"?{parsed.query}"

    requests = 0
    errors = 0
    connection: http.client.HTTPConnection | None = None
    start.wait()

    while time.perf_counter() < deadline:
        try:
            if connection is None:
                connection = connection_type(host, port, timeout=5)
            connection.request("GET", path)
            response = connection.getresponse()
            response.read()
            requests += 1
        except (OSError, http.client.HTTPException):
            errors += 1
            if connection is not None:
                connection.close()
            connection = None

    if connection is not None:
        connection.close()
    return requests, errors


def generate_load(url: str, seconds: int, concurrency: int) -> dict[str, int]:
    """Run exactly concurrency workers; each keeps one request in flight."""
    start = threading.Event()
    deadline = time.perf_counter() + seconds
    with ThreadPoolExecutor(max_workers=concurrency) as executor:
        futures = [
            executor.submit(http_worker, url, deadline, start)
            for _ in range(concurrency)
        ]
        start.set()
        results = [future.result() for future in futures]

    return {
        "requests": sum(result[0] for result in results),
        "errors": sum(result[1] for result in results),
    }


def measure(
    process: psutil.Process,
    url: str,
    seconds: int,
    concurrency: int,
    action: str,
    repetition: int,
    output_directory: Path,
) -> RunResult:
    counters: dict[str, int] = {}
    load_error: BaseException | None = None

    def load_target() -> None:
        nonlocal counters, load_error
        try:
            counters = generate_load(url, seconds, concurrency)
        except BaseException as error:
            load_error = error

    load = threading.Thread(target=load_target, name=f"load-{action}")
    samples: list[dict[str, float | int | str]] = []

    process.cpu_percent(interval=None)
    load.start()
    while load.is_alive():
        cpu_percent = process.cpu_percent(interval=1.0)
        if not load.is_alive():
            break
        rss_mb = process.memory_info().rss / (1024 * 1024)
        samples.append(
            {
                "timestamp": time.time(),
                "action": action,
                "repetition": repetition,
                "cpu_percent_one_core": cpu_percent,
                "cpu_one_core_ratio": cpu_percent / 100.0,
                "rss_mb": rss_mb,
            }
        )
    load.join()

    if load_error is not None:
        raise load_error
    if not samples:
        raise RuntimeError("No CPU or memory samples were collected")

    steady_samples = samples[2:] or samples
    write_dicts(output_directory / f"{action}-{repetition}.csv", steady_samples)

    return RunResult(
        action=action,
        repetition=repetition,
        concurrency=concurrency,
        requests=counters["requests"],
        errors=counters["errors"],
        average_cpu_percent_one_core=statistics.fmean(
            float(row["cpu_percent_one_core"]) for row in steady_samples
        ),
        peak_rss_mb=max(float(row["rss_mb"]) for row in steady_samples),
    )


def write_dicts(path: Path, rows: list[dict[str, object]]) -> None:
    if not rows:
        return
    with path.open("w", newline="", encoding="utf-8") as output:
        writer = csv.DictWriter(output, fieldnames=rows[0].keys())
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pid", required=True, type=int)
    parser.add_argument("--url", default="http://localhost:8080/")
    parser.add_argument("--duration", default=30, type=int)
    parser.add_argument("--repetitions", default=5, type=int)
    parser.add_argument("--concurrency", default=32, type=int)
    parser.add_argument("--output", default="measurements-psutil", type=Path)
    args = parser.parse_args()

    args.output.mkdir(parents=True, exist_ok=True)
    process = psutil.Process(args.pid)

    print("Warming up the server for 5 seconds with Python...", flush=True)
    generate_load(args.url, 5, args.concurrency)

    runs: list[RunResult] = []
    for repetition in range(1, args.repetitions + 1):
        print(
            f"Repetition {repetition}/{args.repetitions} - "
            f"{args.concurrency} concurrent workers",
            flush=True,
        )
        runs.append(
            measure(
                process,
                args.url,
                args.duration,
                args.concurrency,
                "concurrent",
                repetition,
                args.output,
            )
        )
        print(f"Repetition {repetition}/{args.repetitions} - sequential", flush=True)
        runs.append(
            measure(
                process,
                args.url,
                args.duration,
                1,
                "sequential",
                repetition,
                args.output,
            )
        )

    write_dicts(args.output / "cpu-memory-runs.csv", [asdict(run) for run in runs])
    summary: list[dict[str, object]] = []
    for action in ("concurrent", "sequential"):
        action_runs = [run for run in runs if run.action == action]
        summary.append(
            {
                "action": action,
                "repetitions": len(action_runs),
                "concurrency": action_runs[0].concurrency,
                "median_average_cpu_percent_one_core": statistics.median(
                    run.average_cpu_percent_one_core for run in action_runs
                ),
                "median_peak_rss_mb": statistics.median(run.peak_rss_mb for run in action_runs),
                "median_requests": statistics.median(run.requests for run in action_runs),
                "total_errors": sum(run.errors for run in action_runs),
            }
        )

    write_dicts(args.output / "cpu-memory-summary.csv", summary)
    for row in summary:
        print(row, flush=True)


if __name__ == "__main__":
    main()
