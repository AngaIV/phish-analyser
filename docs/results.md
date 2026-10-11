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
| `display-name-spoof` | 1.0 | 10 | 10 | 0 | **1.00** | 0.29 |
| `lookalike-domain` | 1.0 | pending | — | — | — | — |

Rules 4, 6, 7 and 8 not yet implemented. Rule 4 is **deferred** behind an ESP allow-list
(see D-009).

### Combined results — the actual exit gate

Measured over the 70-file development split (35 phishing, 35 legitimate), after the
scorer fix:

| Metric | Value | Target | Status |
|---|---|---|---|
| Phishing covered (≥1 flag) | 20/35 = **57%** | — | Clears U1's floor |
| Phishing amber or red (U1) | 18/35 = **51%** | ≥50% | **Met** |
| False green, no `Unknown` (I1) | 6/35 = **17%** | 0% | **Not met — restated, see `success.md`** |
| False green despite `Unknown` | **0/35** | 0% | Met after scorer fix |
| Legitimate amber/red (P3 FP) | **0/35 = 0%** | under 5% | **Met** |
| Legitimate flagged by ≥1 rule | 1/35 = 3% | — | `ham_007` only, stays green |

**P3 and U1 both pass. I1's residual 17% is the open item.**

**U1 was cleared by a weight change, not a new rule.** `lookalike-domain`'s cheap-TLD and
IP-derived signals carried weight 1, so messages with only those signals scored 1 and
stayed green. Raising them to weight 2 moved three samples to amber: U1 43% → 51%, false
green 20% → 17%, **P3 unchanged at 0%**. The change was simulated against real per-sample
findings before being applied, and the simulation matched the re-measurement exactly.

**The first recommendation on this was wrong.** Working from aggregate counts, I advised
raising `identity-mismatch`'s Return-Path weight instead. Simulating it showed it reaches
only 46% — still short of U1 — *and* pushes `ham_007` to amber, costing 3% on P3 for no
criterion gain. Aggregate arithmetic was enough to produce a plausible answer and not
enough to produce a correct one; per-sample simulation took one script.

**False green was the surprise, and it split into two very different problems.** The
first measurement reported 11/35 (31%), which conflated them:

- **4 samples showed green *despite* an explicit `Unknown` finding** — a genuine fail-safe
  breach. `Scorer.Colour` checked for unknowns only on the zero-flag path, so a weight-1
  finding bypassed the check entirely. Fixed, with a regression test.
- **7 samples produce no flag and no `Unknown`** — every rule ran and returned `Pass`. This
  is an honest miss, not a scoring bug, and **no scoring change can address it.** The
  senders use ordinary real domains (`caresly.com`, `t_users.com`, `swiftactivation.net`,
  `tontransport.ga`, `hw.ac.uk`) with no brand claim and no anomaly.

The 7 are the reason I1's original 0% target was **unachievable**, and why it has been
restated (see `docs/success.md`). Header-only analysis cannot detect phishing that
leaves no header evidence.

**Per-rule overlap is now material.** Summing per-rule recalls gives 0.17 + 0.29 + 0.11 +
0.09 = 0.66, but true combined coverage is 0.57 — five flags are duplicates. Only the union
is quotable.

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

**`display-name-spoof` is the strongest rule so far, and it was nearly free.** At 1.00
precision and 0.29 recall it outperforms both earlier rules combined, on ~120 lines and
zero new dependencies. It also confirms the reframing above: the cheap, high-signal check
was the one the plan listed third, while the elaborate one it listed first
(`auth-results`) catches a sixth as much. **Lesson for the remaining rules: probe the
corpus for the signal's actual yield before writing the machinery.** Two probes during
rule 5 planning killed an expensive feature (edit distance) before a line of it was
written — see D-010.

## Residual issues

| Issue | Impact | Disposition |
|---|---|---|
| `ham_007_world_wide_words.eml` flagged by `identity-mismatch` | 1 FP | Documented. A newsletter with no list headers; not fixable with header-only signals. |
| `auth-results` trusts the first `Authentication-Results` header | Forged header could claim `pass` | Phase 2 rule 8 (authserv-id trust check). Pinned by test. |
| `temperror` and unrecognised values map to `Unknown` | Reduced coverage | Deliberate fail-safe behaviour. |

## Holdout results

Not yet run. Recorded here once, at the end of Phase 2, with no tuning afterwards.