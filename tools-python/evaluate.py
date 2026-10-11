"""Score each rule against the development split: flags, precision, recall."""
import csv
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TESTDATA = ROOT / "testdata"
CLI = ROOT / "engine-dotnet" / "Cli"


def labels():
    with open(TESTDATA / "labels.csv", newline="", encoding="utf-8") as fh:
        return {r["file"]: r["label"] for r in csv.DictReader(fh)}


def files_for(split):
    text = (TESTDATA / "splits" / f"{split}.txt").read_text(encoding="utf-8")
    return [l.strip() for l in text.splitlines() if l.strip()]


def run(split):
    truth = labels()
    stats = {}
    for name in files_for(split):
        folder = "phishing" if truth[name] == "phishing" else "legitimate"
        out = subprocess.run(
            ["dotnet", "run", "--project", str(CLI), "--no-build", "--",
             str(TESTDATA / folder / name), "--json"],
            cwd=ROOT, capture_output=True, text=True, check=True,
        )
        payload = json.loads(out.stdout.splitlines()[-1])
        flagged = {f["rule"] for f in payload["findings"] if f["status"] == "flagged"}
        for rule in flagged:
            entry = stats.setdefault(rule, {"tp": 0, "fp": 0})
            entry["tp" if truth[name] == "phishing" else "fp"] += 1
    return stats, truth


def collect(split):
    truth = labels()
    rows = []
    for name in files_for(split):
        folder = "phishing" if truth[name] == "phishing" else "legitimate"
        out = subprocess.run(
            ["dotnet", "run", "--project", str(CLI), "--no-build", "--",
             str(TESTDATA / folder / name), "--json"],
            cwd=ROOT, capture_output=True, text=True, check=True,
        )
        rows.append((name, truth[name], json.loads(out.stdout.splitlines()[-1])))
    return rows


def main():
    rows = collect("dev")
    phish = [r for r in rows if r[1] == "phishing"]
    legit = [r for r in rows if r[1] == "legitimate"]

    stats = {}
    for name, truth, payload in rows:
        for f in payload["findings"]:
            if f["status"] != "flagged":
                continue
            entry = stats.setdefault(f["rule"], {"tp": 0, "fp": 0})
            entry["tp" if truth == "phishing" else "fp"] += 1

    print(f"{'rule':<22}{'flags':>7}{'TP':>5}{'FP':>5}{'precision':>11}{'recall':>8}")
    for rule, s in sorted(stats.items()):
        tp, fp = s["tp"], s["fp"]
        flags = tp + fp
        print(f"{rule:<22}{flags:>7}{tp:>5}{fp:>5}"
              f"{(tp / flags if flags else 0):>11.2f}{tp / len(phish):>8.2f}")

    covered = [r for r in phish if r[2]["number"] > 0]
    alarmed = [r for r in phish if r[2]["colour"] in ("amber", "red")]
    green = [r for r in phish if r[2]["colour"] == "green"]
    fp_bubble = [r for r in legit if r[2]["colour"] in ("amber", "red")]

    print(f"\n{'COMBINED (the exit gate)':<40}{'value':>12}{'':>10}")
    print(f"{'  phishing covered (>=1 flag)':<40}{len(covered):>6}/{len(phish):<5}"
          f"{len(covered) / len(phish):>9.0%}")
    print(f"{'  phishing amber or red (U1)':<40}{len(alarmed):>6}/{len(phish):<5}"
          f"{len(alarmed) / len(phish):>9.0%}")
    print(f"{'  false green (I1, must be 0)':<40}{len(green):>6}/{len(phish):<5}"
          f"{len(green) / len(phish):>9.0%}")
    print(f"{'  legitimate amber/red (P3 FP)':<40}{len(fp_bubble):>6}/{len(legit):<5}"
          f"{len(fp_bubble) / len(legit):>9.0%}")

    for name, _, payload in fp_bubble:
        print(f"    FP: {name} -> score {payload['score']} {payload['colour']}")


if __name__ == "__main__":
    main()