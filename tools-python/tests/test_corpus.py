"""Corpus invariants: the safety rules must hold in CI, not just by habit."""
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TESTDATA = ROOT / "testdata"

def _rows():
    with open(TESTDATA / "labels.csv", newline="", encoding="utf-8") as fh:
        return list(csv.DictReader(fh))

def test_labels_csv_has_thirty_labelled_samples():
    rows = _rows()
    assert len(rows) == 30
    assert {r["label"] for r in rows} == {"phishing", "legitimate"}

def test_every_labelled_file_exists():
    missing = [r["file"] for r in _rows()
               if not any((TESTDATA / d / r["file"]).exists()
                          for d in ("phishing", "legitimate"))]
    assert missing == [], f"labels.csv references missing files: {missing}"

def test_no_sample_contains_a_body():
    """Sanitised samples are headers-only: a blank line means a body survived."""
    offenders = []
    for d in ("phishing", "legitimate"):
        for f in (TESTDATA / d).glob("*.eml"):
            # a header/body separator outside the final line = unsanitised body
            if "\n\n" in f.read_text(encoding="utf-8", errors="ignore").rstrip("\n"):
                offenders.append(f.name)
    assert offenders == [], f"unsanitised samples: {offenders}"

def test_raw_originals_are_never_committed():
    """The raw corpus is git-ignored; CI must never track files from it."""
    import subprocess
    out = subprocess.run(
        ["git", "ls-files", "testdata/raw"],
        cwd=ROOT, capture_output=True, text=True, check=True,
    ).stdout.strip()
    assert out == "", f"unsanitised originals are tracked in git:\n{out}"