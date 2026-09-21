#!/usr/bin/env python3
"""Build a deterministic runtime-weighted dotnet test filter for one shard.

The script consumes `dotnet test --list-tests` output on stdin. Test classes are
kept intact by default. Explicitly safe, self-resetting heavy classes may be split
at method granularity. A prior TRX timing profile is preferred when available;
otherwise conservative seed weights keep known corpus/bootstrap work from
accumulating in one shard.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path

TEST_CLASS = re.compile(
    r"^\s*(RulesCore\.IntegrationTests\.[A-Za-z_][A-Za-z0-9_`+]*)\."
)
DEFAULT_SECONDS_PER_CASE = 1.0

# Every test in this class creates its own DbContext and brackets its work with
# ResetAsync. Separate CI jobs provide independent PostgreSQL databases, so the
# methods can safely be distributed between shards.
SPLITTABLE_CLASSES = {
    "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests",
}

# These are seed cost estimates, not benchmark claims. They only apply until a
# successful prior run supplies measured TRX durations. The values deliberately
# separate known corpus/bootstrap paths so the initial adaptive run can not put
# nearly all expensive work in one shard merely because the test counts match.
SEED_RUNTIME_SECONDS = {
    "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.FreshDatabaseGetsPublicSrdsAndSettledRulesWithoutOverwritingLaterChanges": 540.0,
    "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.FreshBaselinePublishesNormalizedReviewedCompetencyCorpusToCharacterMechanics": 360.0,
    "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.CompetencyConflictPreventsPartialRulesetPublication": 300.0,
    "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.ExistingSixRuleInstallationGetsIncrementalCompetencyRevisionIdempotently": 300.0,
    "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.BootstrapPreservesRulesLawyerCompetencyDecisionWithoutAdvancingIt": 240.0,
    "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.GenuineReviewedCompetencyConflictRemainsUnresolvedForRulesLawyerAdjudication": 180.0,
    "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.ConcurrentRulesLawyerDecisionWinsInitialCompetencyBaselineRace": 180.0,
    "RulesCore.IntegrationTests.BundledSrdMaintenanceIntegrationTests": 240.0,
    "RulesCore.IntegrationTests.RealSrdImportPilotIntegrationTests": 180.0,
    "RulesCore.IntegrationTests.PcGenSkillConversionIntegrationTests": 120.0,
    "RulesCore.IntegrationTests.PcGenSrdPublicationReconciliationIntegrationTests": 120.0,
    "RulesCore.IntegrationTests.FiveEToolsSplitPublicationIntegrationTests": 120.0,
}


@dataclass(frozen=True, order=True)
class TestGroup:
    kind: str
    name: str

    def filter_term(self) -> str:
        if self.kind == "method":
            return f"FullyQualifiedName={self.name}"
        return f"FullyQualifiedName~{self.name}."


def discover_groups(
    lines: list[str],
) -> tuple[Counter[TestGroup], dict[TestGroup, Counter[str]]]:
    counts: Counter[TestGroup] = Counter()
    tests: dict[TestGroup, Counter[str]] = defaultdict(Counter)

    for line in lines:
        match = TEST_CLASS.match(line)
        if not match:
            continue

        class_name = match.group(1)
        test_name = line.strip().split("(", 1)[0]
        if not test_name.startswith(f"{class_name}."):
            raise ValueError(f"Could not identify test method from: {line.rstrip()}")

        group = (
            TestGroup("method", test_name)
            if class_name in SPLITTABLE_CLASSES
            else TestGroup("class", class_name)
        )
        counts[group] += 1
        tests[group][test_name] += 1

    return counts, tests


def load_timing_profile(path: Path | None) -> dict[str, float]:
    if path is None or not path.is_file():
        return {}

    data = json.loads(path.read_text(encoding="utf-8"))
    raw = data.get("tests", data)
    if not isinstance(raw, dict):
        raise ValueError("Timing profile must contain a 'tests' object.")

    timings: dict[str, float] = {}
    for name, value in raw.items():
        if not isinstance(name, str) or not isinstance(value, (int, float)):
            raise ValueError("Timing profile entries must map test names to seconds.")
        if value < 0:
            raise ValueError("Timing profile durations can not be negative.")
        timings[name] = float(value)
    return timings


def seed_weight(group: TestGroup, test_count: int) -> float:
    return SEED_RUNTIME_SECONDS.get(
        group.name,
        max(DEFAULT_SECONDS_PER_CASE * test_count, DEFAULT_SECONDS_PER_CASE),
    )


def group_weight(
    group: TestGroup,
    test_count: int,
    test_names: Counter[str],
    timings: dict[str, float],
) -> tuple[float, str]:
    measured = 0.0
    missing_cases = 0

    for test_name, case_count in test_names.items():
        if test_name in timings:
            measured += timings[test_name]
        else:
            missing_cases += case_count

    if measured > 0 and missing_cases == 0:
        return max(measured, 0.001), "measured"

    if measured > 0:
        fallback = max(
            DEFAULT_SECONDS_PER_CASE * missing_cases,
            SEED_RUNTIME_SECONDS.get(group.name, 0.0),
        )
        return measured + fallback, "mixed"

    return seed_weight(group, test_count), "seed"


def partition_groups(
    group_counts: Counter[TestGroup],
    group_tests: dict[TestGroup, Counter[str]],
    timings: dict[str, float],
    shard_count: int,
) -> tuple[list[list[TestGroup]], list[float], dict[TestGroup, tuple[float, str]]]:
    if shard_count < 1:
        raise ValueError("shard_count must be positive")

    shards: list[list[TestGroup]] = [[] for _ in range(shard_count)]
    loads = [0.0] * shard_count
    weights = {
        group: group_weight(group, count, group_tests[group], timings)
        for group, count in group_counts.items()
    }

    for group in sorted(
        group_counts,
        key=lambda value: (-weights[value][0], value),
    ):
        shard = min(
            range(shard_count),
            key=lambda index: (loads[index], len(shards[index]), index),
        )
        shards[shard].append(group)
        loads[shard] += weights[group][0]

    for shard in shards:
        shard.sort()

    return shards, loads, weights


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
    counts, tests = discover_groups(sample)
    alpha = TestGroup("class", "RulesCore.IntegrationTests.AlphaTests")
    beta = TestGroup("class", "RulesCore.IntegrationTests.BetaTests")
    first = TestGroup(
        "method",
        "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.First",
    )
    second = TestGroup(
        "method",
        "RulesCore.IntegrationTests.BaselineBootstrapIntegrationTests.Second",
    )

    assert counts[alpha] == 2
    assert counts[beta] == 1
    assert counts[first] == 1
    assert counts[second] == 1

    timings = {
        "RulesCore.IntegrationTests.AlphaTests.First": 30.0,
        "RulesCore.IntegrationTests.AlphaTests.Second": 10.0,
        "RulesCore.IntegrationTests.BetaTests.First": 2.0,
        first.name: 50.0,
        second.name: 1.0,
    }
    shards, loads, weights = partition_groups(counts, tests, timings, 2)
    assert sorted(group for shard in shards for group in shard) == sorted(counts)
    assert all(shard for shard in shards)
    assert weights[alpha] == (40.0, "measured")
    assert weights[first] == (50.0, "measured")
    assert max(loads) - min(loads) <= 13.0

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
    parser.add_argument("--timings", type=Path)
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

    group_counts, group_tests = discover_groups(sys.stdin.readlines())
    if not group_counts:
        print(
            "No RulesCore integration tests were discovered in dotnet --list-tests output.",
            file=sys.stderr,
        )
        return 2

    timings = load_timing_profile(args.timings)
    shards, loads, weights = partition_groups(
        group_counts,
        group_tests,
        timings,
        args.shard_count,
    )
    if any(not shard for shard in shards):
        print(
            f"Discovered only {len(group_counts)} test groups for "
            f"{args.shard_count} shards.",
            file=sys.stderr,
        )
        return 2

    selected = shards[args.shard]
    selected_cases = sum(group_counts[group] for group in selected)
    total_cases = sum(group_counts.values())
    source = "measured profile" if timings else "seed cost profile"
    print(
        f"Integration shard {args.shard + 1}/{args.shard_count}: "
        f"{len(selected)} groups, {selected_cases}/{total_cases} test cases, "
        f"{loads[args.shard]:.1f}s estimated load using {source}.",
        file=sys.stderr,
    )
    print(
        "Estimated shard loads: "
        + ", ".join(f"{index}={load:.1f}s" for index, load in enumerate(loads)),
        file=sys.stderr,
    )
    for group in selected:
        weight, weight_source = weights[group]
        print(
            f"  {group.name} ({group_counts[group]} case"
            f"{'s' if group_counts[group] != 1 else ''}; "
            f"{weight:.1f}s {weight_source})",
            file=sys.stderr,
        )

    print(build_filter(selected))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
