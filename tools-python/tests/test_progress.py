"""Tests for the README progress calculation.

These matter more than they look: the progress bar is public, and a wrong number is a
small but real credibility problem. The status words are matched by substring, so the
cases below are mostly about the ways that goes wrong.
"""

import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[2] / ".github" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from progress import PHASE_WEIGHTS, completion_ratio  # noqa: E402


@pytest.mark.parametrize(
    "status, expected",
    [
        ("Done", 1.0),
        ("done", 1.0),
        ("Complete", 1.0),
        ("Met", 1.0),
        ("In progress", 0.4),
        ("underway", 0.4),
        ("half done", 0.5),
        ("part done", 0.5),
        ("Not started", 0.0),
        ("Planned", 0.0),
        ("Not yet measurable", 0.0),
        ("", 0.0),
        ("something unrecognised", 0.0),
    ],
)
def test_completion_ratio(status, expected):
    assert completion_ratio(status) == expected


def test_not_started_beats_the_started_substring():
    """"Not started" contains "started"; it must not be read as progress."""
    assert completion_ratio("Not started") == 0.0
    assert completion_ratio("Not yet measurable") == 0.0


def test_partial_beats_the_done_substring():
    """"part done" and "half done" contain "done"; they must not read as complete."""
    assert completion_ratio("part done") == 0.5
    assert completion_ratio("half done") == 0.5
    assert completion_ratio("Phase 3 part done") == 0.5


def test_weights_cover_every_phase_in_the_readme():
    # The README's table runs 0..6. A missing weight would silently drop a phase from
    # the total and inflate the percentage.
    assert sorted(PHASE_WEIGHTS) == [0, 1, 2, 3, 4, 5, 6]


def test_weights_are_positive():
    assert all(weight > 0 for weight in PHASE_WEIGHTS.values())
