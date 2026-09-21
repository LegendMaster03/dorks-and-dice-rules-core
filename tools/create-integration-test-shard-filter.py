#!/usr/bin/env python3
"""Build a deterministic dotnet test filter for one integration-test shard.

The script consumes `dotnet test --list-tests` output on stdin. Test classes are
kept intact and greedily balanced by discovered test-case count so that no class
is split between PostgreSQL shards.
"""

from __future__ import annotations

import argparse
import re
import sys
from collections import Counter

TEST_CLASS = re.compile(
    r"^\s*(RulesCore\.IntegrationTests\.[A-Za-z_][A-Za-z0-9_`+]*)\."
)


def discover_classes(lines: list[str]) -> Counter[str]:
    counts: Counter[str] = Counter()
    for line in lines:
        match = TEST_CLASS.match(line)
        if match:
            counts[match.group(1)] += 1
    return counts


def partition_classes(
    class_counts: Counter[str], shard_count: int
) -> list[list[str]]:
    if shard_count < 1:
        raise ValueError("shard_count must be positive")

    shards: list[list[str]] = [[] for _ in range(shard_count)]
    loads = [0] * shard_count

    for class_name, test_count in sorted(
        class_counts.items(), key=lambda item: (-item[1], item[0])
    ):
        shard = min(
            range(shard_count),
            key=lambda index: (loads[index], len(shards[index]), index),
        )
        shards[shard].append(class_name)
        loads[shard] += test_count

    for shard in shards:
        shard.sort()

    return shards


def build_filter(classes: list[str]) -> str:
    return "|".join(f"FullyQualifiedName~{name}." for name in classes)


def self_test() -> None:
    sample = [
        "The following Tests are available:",
        "    RulesCore.IntegrationTests.AlphaTests.First",
        "    RulesCore.IntegrationTests.AlphaTests.Second",
        "    RulesCore.IntegrationTests.BetaTests.First",
        "    RulesCore.IntegrationTests.GammaTests.First(value: 1)",
    ]
    counts = discover_classes(sample)
    assert counts == Counter(
        {
            "RulesCore.IntegrationTests.AlphaTests": 2,
            "RulesCore.IntegrationTests.BetaTests": 1,
            "RulesCore.IntegrationTests.GammaTests": 1,
        }
    )
    shards = partition_classes(counts, 2)
    assert sorted(name for shard in shards for name in shard) == sorted(counts)
    assert all(shard for shard in shards)
    assert "FullyQualifiedName~RulesCore.IntegrationTests.AlphaTests." in build_filter(
        shards[0]
    ) or "FullyQualifiedName~RulesCore.IntegrationTests.AlphaTests." in build_filter(
        shards[1]
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--shard", type=int)
    parser.add_argument("--shard-count", type=int, default=4)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        return 0

    if args.shard is None:
        parser.error("--shard is required unless --self-test is used")
    if args.shard_count < 1:
        parser.error("--shard-count must be positive")
    if not 0 <= args.shard < args.shard_count:
        parser.error("--shard must be in [0, shard-count)")

    class_counts = discover_classes(sys.stdin.readlines())
    if not class_counts:
        print(
            "No RulesCore integration tests were discovered in dotnet --list-tests output.",
            file=sys.stderr,
        )
        return 2

    shards = partition_classes(class_counts, args.shard_count)
    if any(not shard for shard in shards):
        print(
            f"Discovered only {len(class_counts)} test classes for "
            f"{args.shard_count} shards.",
            file=sys.stderr,
        )
        return 2

    selected = shards[args.shard]
    load = sum(class_counts[name] for name in selected)
    total = sum(class_counts.values())
    print(
        f"Integration shard {args.shard + 1}/{args.shard_count}: "
        f"{len(selected)} classes, {load}/{total} discovered test cases.",
        file=sys.stderr,
    )
    print(build_filter(selected))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
