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


def main():
    stats, truth = run("dev")
    n_phish = sum(1 for v in truth.values() if v == "phishing") if len(truth) < 200 else 0
    dev = files_for("dev")
    n_phish = sum(1 for f in dev if truth[f] == "phishing")

    print(f"{'rule':<22}{'flags':>7}{'TP':>5}{'FP':>5}{'precision':>11}{'recall':>8}")
    for rule, s in sorted(stats.items()):
        tp, fp = s["tp"], s["fp"]
        flags = tp + fp
        precision = tp / flags if flags else 0.0
        recall = tp / n_phish if n_phish else 0.0
        print(f"{rule:<22}{flags:>7}{tp:>5}{fp:>5}{precision:>11.2f}{recall:>8.2f}")


if __name__ == "__main__":
    main()