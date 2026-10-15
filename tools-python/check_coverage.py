#!/usr/bin/env python3
"""Check that a measurement is actually measuring something.

Written after D-018. The project reported "0 of 50 legitimate emails flagged" for weeks
before anyone noticed that no legitimate sample carries an `Authentication-Results`
header. The rules were being skipped, not satisfied, and the fail-safe greyed every clean
email before a false positive could be recorded.

A metric computed from samples the rules never evaluated is not a good result, it is an
absent one. This script exists so that failure cannot recur silently: it reports, per rule
and per email type, how many samples reached a real verdict (`pass` or `flagged`) as
opposed to `unknown`.

This is a validity check, not a scoring check. It deliberately does not compute precision
or recall, because those numbers are meaningless when coverage is missing.

Usage:
    python tools-python/check_coverage.py [dev|holdout]

Exit codes:
    0  every rule reached a verdict on a meaningful share of both email types
    1  at least one rule is effectively untested on one of the two types
"""

from __future__ import annotations

import csv
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TESTDATA = ROOT / "testdata"
CLI = ROOT / "engine-dotnet" / "Cli"

# A rule must reach a verdict on at least this share of samples of each type before any
# metric involving it can be trusted. Set low on purpose: the aim is to catch a rule that
# is essentially never evaluated, not to demand high coverage.
MIN_MEASURED_SHARE = 0.25


def labels() -> dict[str, str]:
    with open(TESTDATA / "labels.csv", newline="", encoding="utf-8") as fh:
        return {r["file"]: r["label"] for r in csv.DictReader(fh)}


def files_for(split: str) -> list[str]:
    text = (TESTDATA / "splits" / f"{split}.txt").read_text(encoding="utf-8")
    return [line.strip() for line in text.splitlines() if line.strip()]


def analyse(name: str, label: str) -> dict:
    folder = "phishing" if label == "phishing" else "legitimate"
    completed = subprocess.run(
        ["dotnet", "run", "--project", str(CLI), "--no-build", "--",
         str(TESTDATA / folder / name), "--json"],
        cwd=ROOT, capture_output=True, text=True, check=True,
    )
    return json.loads(completed.stdout.splitlines()[-1])


def main() -> int:
    split = sys.argv[1] if len(sys.argv) > 1 else "dev"
    truth = labels()

    # rule -> label -> status -> count
    tally: dict[str, dict[str, dict[str, int]]] = {}
    totals: dict[str, int] = {"phishing": 0, "legitimate": 0}

    for name in files_for(split):
        label = truth[name]
        totals[label] += 1
        payload = analyse(name, label)

        for finding in payload["findings"]:
            rule = finding["rule"]
            status = finding["status"]
            tally.setdefault(rule, {}).setdefault(label, {})
            tally[rule][label][status] = tally[rule][label].get(status, 0) + 1

    print(f"Coverage validity check - split: {split}")
    print(f"  phishing samples: {totals['phishing']}, legitimate samples: {totals['legitimate']}")
    print()

    header = f"{'rule':<22} {'type':<11} {'pass':>5} {'flag':>5} {'unk':>5} {'measured':>9}"
    print(header)
    print("-" * len(header))

    failures: list[str] = []

    for rule in sorted(tally):
        for label in ("phishing", "legitimate"):
            counts = tally[rule].get(label, {})
            passed = counts.get("pass", 0)
            flagged = counts.get("flagged", 0)
            unknown = counts.get("unknown", 0)
            total = passed + flagged + unknown

            measured = passed + flagged
            share = measured / total if total else 0.0
            print(f"{rule:<22} {label:<11} {passed:>5} {flagged:>5} {unknown:>5} {share:>8.0%}")

            if share < MIN_MEASURED_SHARE:
                failures.append(
                    f"{rule} reached a verdict on only {measured}/{total} {label} samples "
                    f"({share:.0%}); any metric involving it is not measuring anything"
                )
        print()

    if failures:
        print("NOT VALID:")
        for failure in failures:
            print(f"  - {failure}")
        print()
        print("A result drawn from samples the rule never evaluated is an absent result,")
        print("not a good one. See D-018 in docs/decisions.md.")
        return 1

    print("Valid: every rule reached a verdict on a meaningful share of both email types.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
