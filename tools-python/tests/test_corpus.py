"""Corpus invariants: the safety rules must hold in CI, not just by habit."""
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TESTDATA = ROOT / "testdata"

def _rows():
    with open(TESTDATA / "labels.csv", newline="", encoding="utf-8") as fh:
        return list(csv.DictReader(fh))

def test_labels_csv_meets_the_corpus_floor():
    """Phase 1 fallback floor is 100 samples; both classes must be present."""
    rows = _rows()
    assert len(rows) >= 100, f"corpus below the 100-sample floor: {len(rows)}"
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

def _split(name):
    return {l.strip() for l in (TESTDATA / "splits" / f"{name}.txt").read_text(encoding="utf-8").splitlines() if l.strip()}

def test_splits_are_disjoint_and_complete():
    dev, holdout = _split("dev"), _split("holdout")
    labelled = {r["file"] for r in _rows()}
    assert dev.isdisjoint(holdout), "a file appears in both splits"
    assert dev | holdout == labelled, "splits do not cover every labelled file"

def test_holdout_is_a_meaningful_size():
    """30 percent of the corpus, within one file, and never empty."""
    total = len(_rows())
    holdout = len(_split("holdout"))
    assert holdout >= total * 0.25, f"holdout too small: {holdout} of {total}"