# Journal

Weekly review, per the build plan's §5.5 template. One entry per week.
Hours are recorded by the author.

---

## Week of 2026-10-11 — Phase 1 closed, Phase 2 started

### 1. Hours spent this week

_To be filled in by the author._

### 2. What I finished

**Phase 1 (corpus and parser) — exit gate met.**

- Corpus grown from 30 to **100 labelled samples** (50 phishing / 50 legitimate), all
  headers-only with zero body leaks. Sources: Nazario phishing-2023 (CC-BY-4.0) and
  three SpamAssassin archives (`easy_ham`, `easy_ham_2`, `hard_ham`), which is what
  supplies the hard legitimate cases — mailing lists, `Precedence: bulk`, newsletters,
  thread replies and forwards. Those are the false-positive canaries.
- Reproducible **70/30 development/holdout split** (deterministic seed `20261011`),
  with the holdout enforced as untouched by a test.
- Deterministic tooling: `grow_corpus.py`, `rename_corpus.py`, `split_corpus.py`,
  `make_malformed.py`, `evaluate.py`. All 70 auto-named files renamed to
  domain-derived names.
- `SafeMessageLoader` — parse wrapper guaranteeing no exception escapes, a size
  ceiling enforced *before* parsing, and anomalies surfaced as warnings rather than
  thrown. Plus `ParseResult` so failure is a value.
- Parser robustness: 8 malformed fixtures (truncated, duplicate `From`, empty, missing
  colons, binary junk, null bytes, deeply folded, 10 MB header block), a 10 MB
  within-ceiling test, and an oversize-rejection test.
- Python corpus invariants: 6 passing. C#: 33 passing.

**Phase 2 (rules and measurement) — started.**

- Rule 1 `auth-results` (SPF/DKIM/DMARC) implemented, spec'd, 7 unit tests, corpus test.
- Rule 2 `identity-mismatch` implemented, spec'd, 12 tests, **measured and then fixed**
  after its first measurement exposed a serious false-positive problem.
- `evaluate.py` harness producing per-rule precision and recall on the dev split.
- `docs/results.md` results table established.

### 3. What broke or surprised me

- **A stale build produced a wrong measurement.** After applying the rule 2 fix,
  `evaluate.py` reported an identical table — the fix appeared to do nothing. The CLI's
  `Engine.dll` was 13 minutes older than the engine's: `evaluate.py` uses `--no-build`,
  and `dotnet test` does not rebuild the `Cli` project, so the harness was scoring the
  old engine. Rebuilding revealed the real figures (precision 0.27 → 0.80). **Lesson:
  a surprising "no change" after a targeted fix is more likely a stale artefact than a
  genuinely ineffective fix.** Logged as D-007.
- **The predicted rule 2 outcome was worse than the spec anticipated.** The spec listed
  mailing lists as "the single largest FP risk"; the measurement showed 21 of 22 false
  positives were list or bulk mail. Worth noting that writing the FP sources down *in
  advance* made the diagnosis immediate — the measurement matched the prediction, so
  no time was lost deciding whether the corpus was at fault.
- **Fixing precision cost half the recall** (TP 8 → 4). This was not obvious until
  measured, and it is the first hard evidence that header-only identity checks have a
  ceiling.
- **`auth-results` is nearly useless alone**: precision 1.00 but recall 0.17, with 65%
  of mechanism-checks across the corpus returning `Unknown`. Modern phishing passes
  authentication. This reframes the whole project — the lookalike and display-name
  rules are the core signal, not supporting cast.
- **A typo in `labels.csv`** (`phish_015_ups_delivery.phish.eml`) was caught by the
  corpus invariant test on its first run. The invariant test earned its place
  immediately.
- Template leftovers (`Class1.cs`, `UnitTest1.cs`) survived until Phase 1 close.

### 4. Decisions made (and logged)

D-001 web-first header access; D-002 bubble overlay rather than browser extension in
New Outlook; D-003 headers-only sanitisation; D-004 `Unknown` never `Pass`; D-005
weights hardcoded for now; D-006 list/bulk suppression in rule 2; D-007 rebuild before
measuring; D-008 fuzzing deferred to the parser freeze. Full reasoning in
`docs/decisions.md`.

### 5. Risks that changed

- **Increased:** the ceiling on header-only detection. Relying on authentication
  results was the plan's first rule and it covers 17% of phishing. If rules 3–6 do not
  recover coverage, the success criteria may need renegotiating with the stakeholder.
- **Increased:** false-positive pressure from bulk mail. Any future rule touching
  sender identity or reply paths must handle list and bulk mail explicitly, or it will
  repeat rule 2's 0.27 precision.
- **Unchanged:** the authserv-id trust gap (rule 8) — an attacker can inject a forged
  `Authentication-Results: ... pass` header that the current rule would trust.
- **Unchanged:** POPIA/ethics compliance remains a gap in the plan's privacy section;
  flagged for the I&TS stakeholder conversation.
- **Reduced:** parser robustness risk. 33 tests including a 10 MB header block and
  eight malformed fixtures, with no exception escaping the loader.

### 6. Plan for next week (three items maximum)

1. Rule 3 — display-name spoofing (address or brand name in the display name).
2. Rule 4 — `Message-ID` domain and DKIM signing-domain alignment with `From`.
3. Rule 5 — lookalike domains (homoglyphs, mixed scripts, edit distance, suspicious
   TLDs, hyphen tricks), since the measurements say this is where detection must come
   from.

### 7. Am I inside the phase time-box?

Phase 1 completed with its exit gate met on all five criteria; task 8 (light fuzzing)
deferred by decision (D-008), not by overrun. Phase 2 is on schedule but its exit gate
is heavier — 8–10 rules each with a measured precision figure, and an overall
false-positive rate under threshold — so the cut, if needed, is **rule count**: ship
6 well-measured rules rather than 10 unmeasured ones.