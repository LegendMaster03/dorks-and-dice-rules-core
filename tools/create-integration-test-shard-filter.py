#!/usr/bin/env python3
"""Build a deterministic dotnet test filter for one integration-test shard.

The script consumes `dotnet test --list-tests` output on stdin. Test classes are
kept intact by default. Explicitly safe, self-resetting heavy classes may be split
at method granularity so repeated corpus hydration can execute on isolated
PostgreSQL shards instead of serially on one database.
"""

from __future__ import annotations

import argparse
import re
import sys
from collections import Counter
from dataclasses import dataclass

TEST_CLASS = re.compile(
    r"^\s*(RulesCore\.IntegrationTests\.[A-Za-z_][A-Za-z0-9_`+]*)\."
)

# Every test in this class creates its own DbContext and brackets its work with
# ResetAsync. Keeping all seven corpus-heavy bootstrap cases in one shard merely
# serializes repeated fresh baseline hydration; separate CI jobs already provide
# independent PostgreSQL databases.
SPLITTABLE_CLASSES = {
    "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests",
}


@dataclass(frozen=True, order=True)
class TestGroup:
    kind: str
    name: str

    def filter_term(self) -> str:
        if self.kind == "method":
            return f"FullyQualifiedName={self.name}"
        return f"FullyQualifiedName~{self.name}."

    def label(self) -> str:
        return self.name


def discover_groups(lines: list[str]) -> Counter[TestGroup]:
    counts: Counter[TestGroup] = Counter()
    for line in lines:
        match = TEST_CLASS.match(line)
        if not match:
            continue

        class_name = match.group(1)
        if class_name in SPLITTABLE_CLASSES:
            test_name = line.strip().split("(", 1)[0]
            if not test_name.startswith(f"{class_name}."):
                raise ValueError(f"Could not identify test method from: {line.rstrip()}")
            counts[TestGroup("method", test_name)] += 1
        else:
            counts[TestGroup("class", class_name)] += 1

    return counts


def partition_groups(
    group_counts: Counter[TestGroup], shard_count: int
) -> list[list[TestGroup]]:
    if shard_count < 1:
        raise ValueError("shard_count must be positive")

    shards: list[list[TestGroup]] = [[] for _ in range(shard_count)]
    loads = [0] * shard_count

    for group, test_count in sorted(
        group_counts.items(), key=lambda item: (-item[1], item[0])
    ):
        shard = min(
            range(shard_count),
            key=lambda index: (loads[index], len(shards[index]), index),
        )
        shards[shard].append(group)
        loads[shard] += test_count

    for shard in shards:
        shard.sort()

    return shards


def build_filter(groups: list[TestGroup]) -> str:
    return "|".join(group.filter_term() for group in groups)


def self_test() -> None:
    sample = [
        "The following Tests are available:",
        "    RulesCore.IntegrationTests.AlphaTests.First",
        "    RulesCore.IntegrationTests.AlphaTests.Second",
        "    RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.First",
        "    RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.Second",
        "    RulesCore.IntegrationTests.BetaTests.First",
    ]
    counts = discover_groups(sample)
    assert counts[TestGroup("class", "RulesCore.IntegrationTests.AlphaTests")] == 2
    assert counts[TestGroup("class", "RulesCore.IntegrationTests.BetaTests")] == 1
    assert counts[
        TestGroup(
            "method",
            "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.First",
        )
    ] == 1
    assert counts[
        TestGroup(
            "method",
            "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.Second",
        )
    ] == 1

    shards = partition_groups(counts, 2)
    assert sorted(group for shard in shards for group in shard) == sorted(counts)
    assert all(shard for shard in shards)
    combined = "|".join(build_filter(shard) for shard in shards)
    assert "FullyQualifiedName~RulesCore.IntegrationTests.AlphaTests." in combined
    assert (
        "FullyQualifiedName="
        "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.First"
    ) in combined


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

    group_counts = discover_groups(sys.stdin.readlines())
    if not group_counts:
        print(
            "No RulesCore integration tests were discovered in dotnet --list-tests output.",
            file=sys.stderr,
        )
        return 2

    shards = partition_groups(group_counts, args.shard_count)
    if any(not shard for shard in shards):
        print(
            f"Discovered only {len(group_counts)} test groups for "
            f"{args.shard_count} shards.",
            file=sys.stderr,
        )
        return 2

    selected = shards[args.shard]
    load = sum(group_counts[group] for group in selected)
    total = sum(group_counts.values())
    print(
        f"Integration shard {args.shard + 1}/{args.shard_count}: "
        f"{len(selected)} groups, {load}/{total} discovered test cases.",
        file=sys.stderr,
    )
    for group in selected:
        print(
            f"  {group.label()} ({group_counts[group]} test case"
            f"{'s' if group_counts[group] != 1 else ''})",
            file=sys.stderr,
        )
    print(build_filter(selected))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
