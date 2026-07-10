#!/usr/bin/env python3
"""
Benchmark OCR pipeline performance.

Runs the OCR pipeline against a set of test bitmaps for a specified duration
and measures CPU time, OCR calls, frame diffs, and average latency.

Usage:
    python scripts/benchmark-ocr.py --duration 300 --output benchmarks/results-dev.json

Requirements:
    - Application must be built (dotnet build -c Release)
    - Test bitmaps in tests/fixtures/screenshots/
"""

import argparse
import json
import subprocess
import sys
import time
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--duration", type=int, default=300,
                        help="Duration in seconds (default: 300 = 5 min)")
    parser.add_argument("--output", default="benchmarks/results-dev.json",
                        help="Output JSON file")
    parser.add_argument("--exe", default="src/AldurPrice/bin/Release/net9.0-windows/AldurPrice.exe",
                        help="Path to built .exe")
    args = parser.parse_args()

    exe_path = Path(args.exe)
    if not exe_path.exists():
        print(f"ERROR: {exe_path} not found. Build first: dotnet build -c Release")
        sys.exit(1)

    print(f"Benchmarking for {args.duration} seconds...")
    print(f"  exe: {exe_path}")

    # Run application with benchmark mode (TODO: implement --benchmark flag in app)
    start = time.time()
    result = subprocess.run(
        [str(exe_path), "--benchmark", f"--duration={args.duration}"],
        capture_output=True, text=True, timeout=args.duration + 30
    )
    elapsed = time.time() - start

    # Parse benchmark output (TODO: define format)
    output = {
        "version": 1,
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "duration_seconds": elapsed,
        "exit_code": result.returncode,
        "stdout": result.stdout[-5000:],
        "stderr": result.stderr[-5000:],
    }

    out_path = Path(args.output)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(output, indent=2), encoding="utf-8")

    print(f"\nResults saved to {out_path}")
    print(f"Exit code: {result.returncode}")
    print(f"Elapsed: {elapsed:.1f}s")


if __name__ == "__main__":
    main()
