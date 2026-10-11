# Rule: auth-results

**Rule id:** `auth-results`
**Version:** 1.0
**Status:** implemented, measured on the development split

## What it detects

The SPF, DKIM and DMARC outcomes recorded by the receiving server in the
`Authentication-Results` header. Each mechanism is evaluated separately, so one
message can produce up to three findings.

## Why it matters

These headers are the receiving mail server's own verdict on whether the message
was really sent by the domain it claims. A failure means the sending server was
not authorised for that domain — the single strongest structural signal available
from headers alone.

## Severity and weight

| Result value | Status | Severity | Weight | Rationale |
|---|---|---|---|---|
| `pass` | Pass | Info | 0 | The server is authorised and/or the signature validates |
| `fail`, `permerror` | Flagged | Medium | 3 | Definitive authorisation failure |
| `softfail` | Flagged | Low | 1 | Probably unauthorised; weaker than a hard fail |
| `temperror` | Unknown | Info | 0 | Transient DNS problem — not evidence either way |
| `none`, `neutral` | Unknown | Info | 0 | No policy to evaluate, or deliberately no assertion |
| absent from header | Unknown | Info | 0 | Header missing or the mechanism unmentioned |
| any other value | Unknown | Info | 0 | Unrecognised value — never a false pass |

Matching is case-insensitive and boundary-anchored (`\bspf=pass\b`), so `SPF=PASS`
is detected and a mechanism name embedded in a longer token is not.

## Explanation text (non-technical)

- Fail: "The sender's domain failed SPF. The server that sent this message is not authorised to send for this domain."
- Softfail: "The sending server is probably not authorised for this domain."
- None/absent: "No result was recorded for this check (missing header, forwarded mail, or 'none')."

Every explanation is written for a non-technical reader, per the Phase 2 exit gate.

## Known false-positive sources

- **Saved `.eml` files lose the header.** Results are added by the receiving server
  in transit; a file exported from a client often has none. This is the dominant
  cause of `Unknown` results and is a known limitation, not a defect.
- **Forwarded mail** may carry the original server's results rather than the
  forwarder's.
- **`none` is common on legitimate bulk mail** from domains without a DMARC policy.

## Known limitation: authserv-id is not verified

When a message carries more than one `Authentication-Results` header, the rule
takes the **first** match. A sender can inject a forged header claiming `pass`;
without checking which server wrote the results, the forged one could win.

This is deliberately out of scope for v1.0 and is tracked as Phase 2 rule 8
(authserv-id trust check). The current behaviour is pinned by the
`TwoAuthResultsHeaders_FirstWins` test so any change is a visible, intentional
change rather than a silent drift.

## Test cases

`Engine.Tests/Rules/AuthResultsRuleTests.cs` — 7 unit tests covering all-pass,
single failure, `none` mapping to Unknown, absent header, uppercase values,
folded headers, and the multiple-header case.

`Engine.Tests/Corpus/AuthResultsCorpusTests.cs` — runs the rule over the whole
corpus and fails only if a result format could not be interpreted.

## Measured precision and recall

Development split (70 files: 35 phishing, 35 legitimate):

| Flags | True positives | False positives | Precision | Recall |
|---|---|---|---|---|
| 6 | 6 | 0 | 1.00 | 0.17 |

**Precision is perfect; recall is low.** The rule never misfires on legitimate
mail, but it only catches 6 of 35 phishing samples, because most phishing in the
corpus passes or lacks authentication entirely — it is sent from legitimate,
correctly-signed infrastructure or arrives with the header stripped.

Across the whole 100-sample corpus, 194 of 300 mechanism-checks (65%) return
`Unknown`. That is the honest measurement and the reason this rule cannot be the
primary detector: it is a high-confidence, low-coverage signal. The identity,
display-name and lookalike rules carry the detection load.

## Change log

- 1.0 — initial implementation. First-match-wins on duplicate headers, documented above.