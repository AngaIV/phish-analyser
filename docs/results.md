# Measured results

Per-rule accuracy on the labelled corpus. Every figure here is produced by
`tools-python/evaluate.py` against the **development split only**. The holdout set
is not used until a phase ends, and results measured on it are recorded separately
and never tuned against.

## Method

- **Corpus:** `testdata/` — 100 headers-only samples (50 phishing / 50 legitimate).
- **Split:** `testdata/splits/dev.txt` (70 files: 35 phishing, 35 legitimate).
- **Command:** `python tools-python/evaluate.py`
- **Definitions:**
  - *flags* — findings whose status is `Flagged`
  - *true positive* — a flag on a sample labelled `phishing`
  - *false positive* — a flag on a sample labelled `legitimate`
  - *precision* — TP / (TP + FP)
  - *recall* — TP / 35 (total phishing samples in the split)

Precision is the priority metric. A tool that flags legitimate mail is worse than
useless to a user, so a rule that buys recall with false positives is rejected.

> **Methodology warning — rebuild before measuring.** `evaluate.py` invokes the CLI
> with `--no-build`. `dotnet test` does not rebuild the `Cli` project, so running
> it after changing the engine silently scores a **stale `Engine.dll`**. This
> produced a wrong table once during development (the v1.1 fix appeared to have no
> effect). Always `dotnet build` from `engine-dotnet/` before evaluating.

## Per-rule results

| Rule | Version | Flags | TP | FP | Precision | Recall |
|---|---|---|---|---|---|---|
| `auth-results` | 1.0 | 6 | 6 | 0 | **1.00** | 0.17 |
| `identity-mismatch` | 1.0 | 30 | 8 | 22 | 0.27 | 0.23 |
| `identity-mismatch` | 1.1 | 5 | 4 | 1 | **0.80** | 0.11 |

Rules 3–8 not yet implemented.

## What the numbers say

**`auth-results` is high-precision, low-coverage.** It never misfires on legitimate
mail, but it catches only 6 of 35 phishing samples. Modern phishing is frequently
sent from legitimate, correctly-authenticated infrastructure, or arrives with the
header stripped by the client that saved it. Across the whole corpus, 194 of 300
mechanism-checks (65%) return `Unknown`. Authentication alone cannot be the
detector; it is a confidence signal, not a verdict.

**`identity-mismatch` needed a false-positive fix to become usable.** The v1.0
measurement — 22 false positives from 30 flags — was almost entirely mailing-list
and bulk mail (21 of 22), where third-party bounce and reply domains are normal.
Suppressing mismatches on list/bulk mail took precision from 0.27 to 0.80.

**That fix cost half the recall.** True positives fell 8 → 4, because 12 of the 35
development phishing samples carry list or bulk headers. This is a real, documented
trade, not a free win. It is also evidence for a design conclusion: header-only
identity checks have a ceiling, and the lookalike-domain and display-name rules
(rules 3 and 5) will have to carry the detection load.

## Residual issues

| Issue | Impact | Disposition |
|---|---|---|
| `ham_007_world_wide_words.eml` flagged by `identity-mismatch` | 1 FP | Documented. A newsletter with no list headers; not fixable with header-only signals. |
| `auth-results` trusts the first `Authentication-Results` header | Forged header could claim `pass` | Phase 2 rule 8 (authserv-id trust check). Pinned by test. |
| `temperror` and unrecognised values map to `Unknown` | Reduced coverage | Deliberate fail-safe behaviour. |

## Holdout results

Not yet run. Recorded here once, at the end of Phase 2, with no tuning afterwards.