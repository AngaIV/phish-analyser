"""Create a stratified 70/30 dev/holdout split of the labelled corpus.

Deterministic: the seed below fixes the assignment, so the split is
reproducible and the holdout stays untouched between runs.
"""
import csv
import random
from pathlib import Path

SEED = 20261011
TESTDATA = Path(__file__).resolve().parents[1] / "testdata"
SPLITS = TESTDATA / "splits"


def main() -> None:
    with open(TESTDATA / "labels.csv", newline="", encoding="utf-8") as fh:
        rows = list(csv.DictReader(fh))

    rng = random.Random(SEED)
    dev: list[dict] = []
    holdout: list[dict] = []

    for label in sorted({r["label"] for r in rows}):
        group = sorted((r for r in rows if r["label"] == label), key=lambda r: r["file"])
        rng.shuffle(group)
        cut = round(len(group) * 0.7)
        dev += group[:cut]
        holdout += group[cut:]

    SPLITS.mkdir(parents=True, exist_ok=True)
    for name, items in (("dev", dev), ("holdout", holdout)):
        text = "".join(f"{i['file']}\n" for i in items)
        (SPLITS / f"{name}.txt").write_text(text, encoding="utf-8")
        print(f"{name}: {len(items)} files")


if __name__ == "__main__":
    main()