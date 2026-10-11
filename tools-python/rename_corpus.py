"""Rename auto-generated corpus files to descriptive, domain-derived names.

Reads labels.csv, renames any file whose name is a placeholder pattern
(srcNNNNN for phishing, bare digits for ham) using the sender domain,
then rewrites labels.csv. Idempotent: already-descriptive names are kept.
"""
import csv
import email
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TESTDATA = ROOT / "testdata"
PLACEHOLDER = re.compile(r"^(phish|ham)_(\d{3})_(src\d+|\d+)\.eml(\.eml)?$", re.I)


def slug(text: str, limit: int = 28) -> str:
    text = re.sub(r"[^a-z0-9]+", "_", text.lower()).strip("_")
    return text[:limit].strip("_")


def domain_of(msg) -> str:
    m = re.search(r"@([A-Za-z0-9.-]+)", msg.get("From", "") or "")
    if not m:
        return "unknown"
    host = m.group(1).lower().rstrip(".")
    parts = [p for p in host.split(".") if p]
    # registrable-ish label: second level, or third from last for co.za-style
    if len(parts) >= 3 and parts[-2] in {"co", "com", "org", "net", "gov", "ac"}:
        return slug(parts[-3])
    return slug(parts[-2]) if len(parts) >= 2 else slug(host)


def main() -> None:
    with open(TESTDATA / "labels.csv", newline="", encoding="utf-8") as fh:
        rows = list(csv.DictReader(fh))

    renamed = 0
    for row in rows:
        old = row["file"]
        m = PLACEHOLDER.match(old)
        if not m:
            continue
        label, num = m.group(1).lower(), m.group(2)
        folder = "phishing" if label == "phish" else "legitimate"
        src = TESTDATA / folder / old
        if not src.exists():
            continue
        msg = email.message_from_bytes(src.read_bytes())
        new = f"{label}_{num}_{domain_of(msg)}.eml"
        dst = TESTDATA / folder / new
        if dst.exists() and dst != src:
            new = f"{label}_{num}_{domain_of(msg)}_b.eml"
            dst = TESTDATA / folder / new
        src.rename(dst)
        row["file"] = new
        renamed += 1

    with open(TESTDATA / "labels.csv", "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=["file", "label", "source", "confidence", "notes"])
        w.writeheader()
        w.writerows(rows)
    print(f"renamed {renamed} files")


if __name__ == "__main__":
    main()