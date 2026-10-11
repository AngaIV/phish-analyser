# Rule specifications

One file per rule. Written **before** implementation (Phase 2 routine, step 1), then
updated with measured results after the dev-set run (step 6).

## Naming and structure

`spec/rules/<rule-id>.md`, where `<rule-id>` matches `IRule.RuleId` exactly, so the
spec, the code and the test file are all findable from one string.

Every spec contains:

1. **What it detects** — the header evidence used.
2. **Why it matters** — the attack this defeats.
3. **Severity and weight** — a table mapping every outcome to a status, severity and weight, including the `Unknown` cases.
4. **Explanation text** — the exact non-technical strings shown to the user (a Phase 2 exit gate).
5. **Known false-positive sources** — written honestly, before measurement, so the measured FPs can be compared against what was predicted.
6. **Measured precision and recall** — filled in from `docs/results.md`.
7. **Change log** — version history, including why a version changed.

## Rules

| # | Rule id | Spec | Status | Precision (dev) |
|---|---|---|---|---|
| 1 | `auth-results` | [auth-results.md](auth-results.md) | Implemented, measured | 1.00 |
| 2 | `identity-mismatch` | [identity-mismatch.md](identity-mismatch.md) | Implemented, measured (v1.1) | 0.80 |
| 3 | `display-name-spoof` | — | Not started | — |
| 4 | `message-id-alignment` | — | Not started | — |
| 5 | `lookalike-domain` | — | Not started | — |
| 6 | `received-chain` | — | Not started | — |
| 7 | `structural-oddities` | — | Not started | — |
| 8 | `authserv-trust` | — | Not started | — |

The build order is easiest-and-highest-value first, per the Phase 2 plan.

## The `Unknown` principle

Every spec must state what happens when its evidence is missing. The rule is:

> Missing or uninterpretable evidence produces `Unknown`, **never** `Pass`.

The single approved exception must be justified in the spec: `identity-mismatch`
treats a missing `Reply-To` as a `Pass`, because the header is genuinely optional
and absent on most legitimate mail. Marking it `Unknown` would add noise without
adding information. Any further exception needs the same reasoning in writing.