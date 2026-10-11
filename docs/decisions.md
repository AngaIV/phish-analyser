# Decision log

One entry per decision that would be expensive to reverse or that a reader might
otherwise question. Newest last. Format: what was decided, why, what was rejected,
and what would make us revisit it.

---

## D-001 — Web-first: Office.js `getAllInternetHeadersAsync()` before COM/VSTO

**Decided:** Build the header-acquisition path on Office.js Mailbox requirement set
1.8 first, rather than starting with a classic Outlook COM/VSTO add-in.

**Why:** One implementation covers New Outlook on Windows, Outlook on the web,
classic Outlook, macOS and mobile 4.2405.0+. The COM path covers a single client.

**Evidence:** `getAllInternetHeadersAsync()` requires Mailbox 1.8, Read mode, the
narrowest `read item` permission, and is supported across all the target surfaces.
`getAsFileAsync()` was also viable (returns the full base64 EML) but needs a higher
requirement set and hands over more data than the analyser needs.

**Rejected:** `item.internetHeaders.getAsync/setAsync` — a trap worth recording,
because it *looks* like the header API. It is **Compose-mode only** and operates on
custom headers you set yourself, so it cannot read a received message at all.

**Revisit if:** Office.js header access proves unavailable or the requirement set
cannot be met on a target surface in practice.

---

## D-002 — The bubble cannot be a browser extension in New Outlook

**Decided:** The Grammarly-style overlay is a WPF window that tracks the target
application's window bounds.

**Why:** New Outlook on Windows is a **WebView2 host**, so there is no browser
extension surface to inject into. A Chrome/Edge extension could not draw on it.

**Consequence:** The browser extension remains the delivery mechanism for
Gmail and other *browser* webmail, but it is a separate surface sharing the same
engine. Three surfaces, one engine.

**Revisit if:** Microsoft exposes an extension or overlay point for New Outlook.

---

## D-003 — Corpus is headers-only, sanitised at the first blank line

**Decided:** Every committed `.eml` sample is truncated at the first blank line, so
only headers remain. Raw originals stay in the git-ignored `testdata/raw/`.

**Why:** The engine analyses headers only, so bodies add no analytical value while
adding real risk — live tracking URLs, working credential-harvesting links, personal
data, and the Nazario 2023+ archives are explicitly *not* anonymised.

**Verification:** Committed samples were checked for zero body leaks, and
`testdata/raw/` was confirmed untracked with `git check-ignore -v`.

**Rule:** Samples are only ever opened as text, never in a mail client.

---

## D-004 — Missing evidence maps to `Unknown`, never to `Pass`

**Decided:** The engine's fail-safe default is `Unknown` (`RuleStatus.Unknown`).
Absent, unparseable or unrecognised evidence never produces a green result.

**Why:** A false pass is the one failure mode that actively harms the user — it
tells them a hostile message is safe. A false `Unknown` merely withholds a verdict.

**Approved exception:** `identity-mismatch` treats a **missing `Reply-To` as a
`Pass`**, because the header is genuinely optional and absent on most legitimate
mail; marking it `Unknown` would add noise without adding information. The
distinction from `Return-Path` — which *is* Unknown when absent, since the receiving
server always adds it — is deliberate and documented in the rule spec.

**Consequence:** Across the corpus, 194 of 300 authentication mechanism-checks
return `Unknown`. That is designed behaviour, not a defect.

---

## D-005 — Rule weights are hardcoded, with configuration deferred

**Decided:** Severity and weight live as literals in each rule for now. Moving them
to configuration files is deferred.

**Why:** Tuning weights before there is a scorer and measured results is guesswork.
The plan calls for configuration-driven weights, and that remains the destination.

**Revisit:** When the scoring module lands and per-rule measurement makes tuning
meaningful.

---

## D-006 — Suppress identity mismatches on list and bulk mail (rule v1.1)

**Decided:** `identity-mismatch` treats a `From`/`Return-Path`/`Reply-To` mismatch as
a **Pass** when the message carries `List-Id`, `List-Unsubscribe`, `List-Post`, or
`Precedence: bulk|list`.

**Why:** The first dev-set measurement produced **22 false positives from 30 flags
(precision 0.27)**, and 21 of the 22 were mailing-list or bulk mail, where a
third-party bounce or reply domain is entirely normal. A rule that flags roughly a
third of legitimate mail is unusable.

**Result:** Precision 0.27 → **0.80**; flags 30 → 5; false positives 22 → 1.

**Cost, accepted knowingly:** Recall fell 0.23 → 0.11 (true positives 8 → 4), because
12 of the 35 development phishing samples carry list or bulk headers. Real phishing
campaigns do abuse bulk-mail infrastructure. The trade was taken because
false positives on legitimate mail are the primary usability failure.

**Revisit if:** Recall on this rule matters more than precision once rules 3 and 5
(display-name, lookalike) are carrying detection load — suppression could be
narrowed to `List-Id` only, or the finding downgraded to `Unknown` rather than `Pass`.

---

## D-007 — Measurement must rebuild before scoring

**Decided:** Always `dotnet build` from `engine-dotnet/` before running
`tools-python/evaluate.py`.

**Why:** `evaluate.py` invokes the CLI with `--no-build`, and `dotnet test` does not
rebuild the `Cli` project. Measuring after an engine change silently scores a stale
`Engine.dll`. **This actually happened** — the v1.1 fix initially appeared to have no
effect because the CLI was running an `Engine.dll` 13 minutes older than the engine
build, and the unchanged table was briefly taken at face value.

**Guard:** The warning is recorded in `docs/results.md` next to the method. A future
improvement is to have `evaluate.py` build the CLI itself before scoring.

---

## D-008 — Light fuzzing deferred until the parser surface freezes

**Decided:** Phase 1 task 8 (light fuzzing) is deferred rather than skipped.

**Why:** The parser surface grows through Phase 2 — `DomainHelper`, `MessageContext`
and later Received-chain parsing all consume untrusted headers. Fuzzing now would
mean fuzzing twice. It belongs immediately before the parser freezes for the
TypeScript port in Phase 4.

**Interim coverage:** The `SafeMessageLoader` malformed-input theory (8 fixtures),
the 10 MB header-block test, the oversize-rejection test, and a deliberately broad
`catch` already provide the durability guarantee; fuzzing's job is to discover new
cases to add to that theory, not to establish it.

**When it runs:** Locally and capped (~20 samples × 50 mutations), kept out of the
per-PR CI run, since fuzzing is inherently variable and a flaky red build costs more
than it catches.