#!/usr/bin/env python3
"""Recalculate the README progress bar from the project's own notes.

The point of doing this in code rather than editing a number by hand is that the bar
cannot drift away from reality. It reads the phase table out of `docs/success.md`,
which is the document that already tracks completion, and converts it into a shields.io
endpoint payload.

If the documentation is wrong, the bar is wrong -- which is the correct failure mode,
because it points at the thing that needs fixing.

Exit codes follow the repo convention: non-zero means the workflow should stop.
"""

from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SUCCESS_DOC = REPO / "docs" / "success.md"
OUTPUT = REPO / "progress.json"
SVG_OUTPUT = REPO / "progress.svg"

# Palette. The green matches the desktop bubble's safe colour so the bar and the app
# read as the same product.
BAR_GREEN = "#0EA47A"
BAR_TRACK = "#E6E8EB"
BAR_TEXT = "#57606A"

# Geometry. Wide and short, so it reads as a bar rather than a badge.
BAR_WIDTH = 760
BAR_HEIGHT = 30
BAR_RADIUS = 15
TEXT_GUTTER = 64
SVG_HEIGHT = 36

# Weight of each phase towards the overall bar. Later phases are worth more because they
# contain more work -- an unweighted count would make the early planning phases look far
# more significant than they were.
PHASE_WEIGHTS = {
    0: 5,
    1: 20,
    2: 30,
    3: 25,
    4: 15,
    5: 5,
    6: 10,
}

# A phase row looks like:  | 2 | Detection rules, scoring and measurement | ✅ Done |
PHASE_ROW = re.compile(
    r"^\|\s*(?P<phase>\d)\s*\|(?P<what>[^|]+)\|(?P<status>[^|]+)\|",
    re.MULTILINE,
)

DONE_MARKERS = ("done", "complete", "met")

# Checked first, because "Not started" contains "started" and would otherwise be read as
# progress. Order matters more than the marker lists here.
NOT_STARTED_MARKERS = ("not started", "planned", "todo", "not yet")

# Checked before the done markers, because "part done" and "half done" both contain
# "done" and would otherwise score as fully complete.
PARTIAL_MARKERS = ("in progress", "underway", "half", "part")


def _contains(text: str, markers: tuple[str, ...]) -> bool:
    """Whole-word match, not a substring match.

    Substring matching looked fine until a test caught "something" containing "met" and
    scoring as a finished phase. Word boundaries are the difference between a progress
    bar and a liability.
    """
    return any(re.search(rf"\b{re.escape(marker)}\b", text) for marker in markers)


def completion_ratio(status: str) -> float:
    """How complete a phase is, judged from the words in its status cell.

    Deliberately conservative: anything not clearly finished scores below 1, and
    anything not clearly started scores 0. Markers are checked from most to least
    specific, and matched on word boundaries.
    """
    text = status.strip().lower()

    if _contains(text, NOT_STARTED_MARKERS):
        return 0.0

    if _contains(text, PARTIAL_MARKERS):
        return 0.5 if _contains(text, ("half", "part")) else 0.4

    if _contains(text, DONE_MARKERS):
        return 1.0

    return 0.0


def read_phases() -> list[tuple[int, str, float]]:
    if not SUCCESS_DOC.exists():
        raise SystemExit(f"error: {SUCCESS_DOC} not found")

    text = SUCCESS_DOC.read_text(encoding="utf-8")

    phases: list[tuple[int, str, float]] = []
    for match in PHASE_ROW.finditer(text):
        number = int(match.group("phase"))
        if number not in PHASE_WEIGHTS:
            continue
        phases.append((number, match.group("what").strip(), completion_ratio(match.group("status"))))

    return phases


def find_readme_phases() -> list[tuple[int, str, float]]:
    """Fall back to the README's own progress table when the docs have no phase rows.

    The README is the public-facing copy, so it is a reasonable second source; using it
    keeps the bar working if the internal document is reorganised.
    """
    readme = REPO / "README.md"
    if not readme.exists():
        return []

    text = readme.read_text(encoding="utf-8")
    phases: list[tuple[int, str, float]] = []

    row = re.compile(r"^\|\s*(?P<phase>\d)\s*\|(?P<what>[^|]+)\|(?P<status>[^|]+)\|", re.MULTILINE)
    for match in row.finditer(text):
        number = int(match.group("phase"))
        if number not in PHASE_WEIGHTS:
            continue
        phases.append((number, match.group("what").strip(), completion_ratio(match.group("status"))))

    return phases


def render_svg(percent: int, phases: list[tuple[int, str, float]]) -> str:
    """Draw the progress bar as a self-contained SVG.

    A filled rectangle rather than a badge, because a percentage is easier to read at a
    glance when you can see how much of the bar is left. Drawn with plain rectangles so
    GitHub renders it identically everywhere: no fonts beyond the system stack, no
    scripts, no external references.
    """
    track_width = BAR_WIDTH - TEXT_GUTTER
    filled = round(track_width * percent / 100)

    # Keep a minimum visible nub once any work is done, so a low percentage is not
    # mistaken for an empty bar.
    if percent > 0:
        filled = max(filled, BAR_RADIUS * 2)

    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="{BAR_WIDTH}" height="{SVG_HEIGHT}" viewBox="0 0 {BAR_WIDTH} {SVG_HEIGHT}" role="img" aria-label="Project progress: {percent} percent">
  <title>Project progress: {percent} percent</title>
  <rect x="0" y="3" width="{track_width}" height="{BAR_HEIGHT}" rx="{BAR_RADIUS}" fill="{BAR_TRACK}"/>
  <clipPath id="fill">
    <rect x="0" y="3" width="{track_width}" height="{BAR_HEIGHT}" rx="{BAR_RADIUS}"/>
  </clipPath>
  <rect x="0" y="3" width="{filled}" height="{BAR_HEIGHT}" fill="{BAR_GREEN}" clip-path="url(#fill)"/>
  <text x="{BAR_WIDTH - 6}" y="{SVG_HEIGHT // 2 + 1}" text-anchor="end" dominant-baseline="middle" font-family="-apple-system, BlinkMacSystemFont, Segoe UI, Helvetica, Arial, sans-serif" font-size="15" font-weight="600" fill="{BAR_TEXT}">{percent}%</text>
</svg>
"""


def main() -> int:
    phases = read_phases() or find_readme_phases()
    if not phases:
        raise SystemExit("error: no phase rows found in docs/success.md or README.md")

    total_weight = sum(PHASE_WEIGHTS[number] for number, _, _ in phases)
    earned = sum(PHASE_WEIGHTS[number] * ratio for number, _, ratio in phases)
    percent = round(earned / total_weight * 100)

    done = sum(1 for _, _, ratio in phases if ratio >= 1.0)
    active = [what for _, what, ratio in phases if 0 < ratio < 1]

    colour = "red" if percent < 34 else "orange" if percent < 67 else "yellow" if percent < 90 else "brightgreen"

    label = f"Phase {max(n for n, _, _ in phases)}"
    message = f"{percent}% complete"

    payload = {
        "schemaVersion": 1,
        "label": "progress",
        "message": f"{percent}%",
        "color": colour,
    }

    OUTPUT.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    SVG_OUTPUT.write_text(render_svg(percent, phases), encoding="utf-8")

    summary = (
        f"{percent}% complete | {done}/{len(phases)} phases done"
        + (f" | in progress: {active[0]}" if active else "")
    )
    print(f"progress: {summary}")
    print(f"wrote {OUTPUT}")
    print(f"wrote {SVG_OUTPUT}")

    # Surface the result to the workflow so the commit message and any later step can use it.
    github_output = os.environ.get("GITHUB_OUTPUT")
    if github_output:
        with open(github_output, "a", encoding="utf-8") as handle:
            handle.write(f"percent={percent}\n")
            handle.write(f"message={summary}\n")

    return 0


if __name__ == "__main__":
    sys.exit(main())
