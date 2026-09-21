#!/usr/bin/env python3
"""Aggregate xUnit/VSTest TRX durations into a reusable integration timing profile."""

from __future__ import annotations

import argparse
import json
import tempfile
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def parse_duration(value: str) -> float:
    hours, minutes, seconds = value.split(":", 2)
    return int(hours) * 3600 + int(minutes) * 60 + float(seconds)


def extract_timings(path: Path) -> dict[str, float]:
    root = ET.parse(path).getroot()
    definitions: dict[str, str] = {}

    for element in root.iter():
        if local_name(element.tag) != "UnitTest":
            continue
        test_id = element.attrib.get("id")
        if not test_id:
            continue
        method = next(
            (
                child
                for child in element.iter()
                if local_name(child.tag) == "TestMethod"
            ),
            None,
        )
        if method is None:
            continue
        class_name = method.attrib.get("className")
        method_name = method.attrib.get("name")
        if class_name and method_name:
            definitions[test_id] = f"{class_name}.{method_name}"

    timings: dict[str, float] = defaultdict(float)
    for element in root.iter():
        if local_name(element.tag) != "UnitTestResult":
            continue
        test_id = element.attrib.get("testId")
        duration = element.attrib.get("duration")
        if not test_id or not duration or test_id not in definitions:
            continue
        timings[definitions[test_id]] += parse_duration(duration)

    return dict(timings)


def merge_timings(paths: list[Path]) -> dict[str, float]:
    merged: dict[str, float] = defaultdict(float)
    for path in paths:
        for test_name, duration in extract_timings(path).items():
            merged[test_name] += duration
    return dict(merged)


def write_profile(path: Path, timings: dict[str, float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "version": 1,
        "tests": {
            name: round(duration, 6)
            for name, duration in sorted(timings.items())
        },
    }
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def print_summary(timings: dict[str, float], limit: int = 20) -> None:
    total = sum(timings.values())
    print(
        f"Collected timing for {len(timings)} integration test methods "
        f"({total:.1f}s cumulative test time)."
    )
    print("")
    print("Slowest integration test methods:")
    for test_name, duration in sorted(
        timings.items(), key=lambda item: (-item[1], item[0])
    )[:limit]:
        print(f"- {duration:8.2f}s  {test_name}")


def self_test() -> None:
    trx = """<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <TestDefinitions>
    <UnitTest id="alpha">
      <TestMethod className="RulesCore.IntegrationTests.AlphaTests" name="First" />
    </UnitTest>
    <UnitTest id="beta">
      <TestMethod className="RulesCore.IntegrationTests.BetaTests" name="Second" />
    </UnitTest>
  </TestDefinitions>
  <Results>
    <UnitTestResult testId="alpha" duration="00:00:01.2500000" outcome="Passed" />
    <UnitTestResult testId="beta" duration="00:01:02.5000000" outcome="Passed" />
  </Results>
</TestRun>
"""
    with tempfile.TemporaryDirectory() as temp_dir:
        path = Path(temp_dir) / "sample.trx"
        path.write_text(trx, encoding="utf-8")
        timings = extract_timings(path)
        assert timings[
            "RulesCore.IntegrationTests.AlphaTests.First"
        ] == 1.25
        assert timings[
            "RulesCore.IntegrationTests.BetaTests.Second"
        ] == 62.5


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("trx", nargs="*", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        return 0

    if not args.trx:
        parser.error("at least one TRX file is required")
    if args.output is None:
        parser.error("--output is required")

    missing = [path for path in args.trx if not path.is_file()]
    if missing:
        parser.error(f"TRX files do not exist: {', '.join(map(str, missing))}")

    timings = merge_timings(args.trx)
    if not timings:
        raise SystemExit("No test timings were found in the supplied TRX files.")

    write_profile(args.output, timings)
    print_summary(timings)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
