"""Corpus validity checks.

These do not test code. They test whether the corpus can support the claims made about
it, which is a different question and the one that went unasked for too long.

Background (D-018): the project reported "0 of 50 legitimate emails flagged" for weeks.
No legitimate sample carries an `Authentication-Results` header, so the rule that reads it
was skipped on every clean email and the fail-safe greyed the bubble before a false
positive could be recorded. The 0% figure measured an absence of testing, not an absence
of false positives.
"""

import csv
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
TESTDATA = ROOT / "testdata"

# The header a receiving mail server stamps with its SPF/DKIM/DMARC verdict.
AUTH_HEADER = "Authentication-Results:"


def labels() -> list[tuple[str, str]]:
    with open(TESTDATA / "labels.csv", newline="", encoding="utf-8") as fh:
        return [(r["file"], r["label"]) for r in csv.DictReader(fh)]


def header_block(name: str, label: str) -> str:
    folder = "phishing" if label == "phishing" else "legitimate"
    path = TESTDATA / folder / name
    if not path.exists():
        pytest.skip(f"{name} not present")
    # Headers only; the corpus stores no bodies.
    return path.read_text(encoding="utf-8", errors="replace")[:8192]


def test_corpus_is_labelled_50_50():
    rows = labels()
    phishing = [r for r in rows if r[1] == "phishing"]
    legitimate = [r for r in rows if r[1] == "legitimate"]
    assert len(phishing) == 50
    assert len(legitimate) == 50


@pytest.mark.xfail(
    reason="D-018: no legitimate sample carries Authentication-Results, so any "
           "false-positive rate measured on this corpus is not measuring anything. "
           "Remove this marker once a modern legitimate corpus is added.",
    strict=True,
)
def test_legitimate_samples_can_exercise_the_auth_results_rule():
    """A false-positive rate is only meaningful if the rule runs on clean mail.

    This is expected to fail until the legitimate half of the corpus is replaced with
    mail that carries `Authentication-Results`. It is marked xfail rather than deleted so
    the gap stays visible in every CI run.
    """
    missing = [
        name for name, label in labels()
        if label == "legitimate" and AUTH_HEADER not in header_block(name, label)
    ]
    assert not missing, (
        f"{len(missing)} legitimate samples have no {AUTH_HEADER} header, so the "
        f"auth-results rule cannot run on them. First few: {missing[:5]}"
    )


def test_phishing_samples_mostly_carry_the_header():
    """Confirms the asymmetry is real and is documented rather than accidental.

    Phishing samples come from a modern archive; legitimate samples are from 2003. If
    this ever fails, the corpus has changed and D-018 needs revisiting.
    """
    rows = [(n, l) for n, l in labels() if l == "phishing"]
    with_header = [n for n, l in rows if AUTH_HEADER in header_block(n, l)]
    assert len(with_header) >= len(rows) * 0.8
