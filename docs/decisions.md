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

---

## D-009 — Rule 4 (`message-id-alignment`) deferred behind an ESP allow-list

**Decided:** Rule 4 is **not** built next, despite the plan listing it fourth. It is
deferred until it can consult an allow-list of legitimate sending platforms.

**Why:** A probe over the 18 phishing samples that rules 1–3 miss showed rule 4 would
have caught **7 of them** — which looked like an easy win until the Message-ID domains
were inspected:

| Message-ID domain | What it actually is |
|---|---|
| `eu-west-2.amazonses.com` | Amazon SES |
| `geopod-ismtpd-4-1` | SendGrid |
| `schoolm.smtp.e.sparkpost.com` | SparkPost |
| `webmail.emailsrvr.com` | Rackspace |
| `desktop-0nldaac…` | Google desktop client |

**Every one of those 7 catches is legitimate sending infrastructure.** `Message-ID` is
stamped by the platform that sends the mail, not by the brand, so a `Message-ID`-versus-
`From` mismatch is *normal* for any organisation using an email platform — which is
almost all of them. Building rule 4 as written would have flagged a large share of
legitimate bulk mail, repeating rule 2's 0.27-precision failure and likely worse.

**Rejected alternative:** implementing it anyway and tuning the weight down. The signal
is not weak, it is *inverted* — it fires on the normal case. A lower weight just makes a
useless finding quieter.

**Revisit when:** an allow-list of ESP and client domains exists to subtract from the
comparison. Then the remaining mismatches are genuinely informative.

**Lesson:** a rule's *catch count* says nothing until you look at *what* it caught. The
probe cost one script and prevented building the wrong rule.

---

## D-010 — Edit distance and homoglyph matching rejected for rule 5

**Decided:** `lookalike-domain` v1.0 implements **three** signals — a brand plus a lure
word in a host label, cheap TLDs, and IP-derived or digit-heavy labels. It deliberately
omits **edit-distance (typosquat) matching**, **homoglyph / mixed-script detection** and
bare **hyphen heuristics**, all of which the plan's rule 5 lists.

**Why — edit distance:** probed at Levenshtein ≤2 against the brand list, it flagged 5
phishing samples but **2 legitimate ones**, and the "hits" were nonsense:

| Domain | Matched | Reality |
|---|---|---|
| `aaps.k12.mi.us` | `ups` | A US school district |
| `hw.ac.uk` | `dhl` | Heriot-Watt University |
| `ucd.ie` | `ups` | University College Dublin (false positive) |
| `users.sourceforge.net` | `usps` | SourceForge (false positive) |

The cause is brand length: `ups`, `dhl` and `fnb` are three characters, so at distance
≤2 they match almost any short domain. Restricting to brands of ≥6 characters and
distance 1 removed both false positives — **and removed all the true positives too,
yielding zero catches.** The classic typosquats (`paypa1.com`, `rnicrosoft.com`) simply
do not occur in this corpus, so the machinery would have been unvalidated code earning
nothing.

**Why — homoglyphs:** detecting them properly requires decoding punycode, because IDN
domains are normally converted to `xn--` form before the engine ever sees them. A naive
"contains non-ASCII" check would therefore rarely fire on real mail while penalising
legitimate international domains. The punycode lookalike `xn--pypal-4ve.com` is **not
detected**, and that is documented as a known limitation rather than half-implemented.

**Why — hyphen heuristics:** legitimate domains use hyphens constantly
(`users.sourceforge.net`). Only hyphens that *join a brand to a lure word*
(`paypal-secure.com`) are treated as evidence, via signal 1.

**What was kept, and why it is honest to keep it:** brand-plus-lure-word matching earns
its place despite catching **zero** development samples, because it targets the dominant
real-world phishing pattern (`paypal-secure.com`, `apple-id-verify.com`) and is unit-
tested from hand-made snippets. It is explicitly recorded as **corpus-unvalidated**.

**Consequence for the measurement:** rule 5's measured recall on this corpus (3 of 35)
comes entirely from cheap TLDs and IP-derived labels. The high-confidence signal
contributes nothing to the dev-set figure, and its real-world false-positive rate is
**unmeasured**. Stated in the rule spec and in `docs/results.md`.

---

## D-011 — False green is restated as 0%-where-checked, with a documented 20% residual

**Decided:** Success criterion I1 changes from *"0% false green on phishing"* to *"0%
false green on messages where no rule returned `Unknown`"*, with the measured residual
**7/35 (20%)** on the dev split recorded as a known, unfixable limitation.

**Why:** The original criterion **was never achievable and should not have been
asserted**. Seven development phishing samples leave no header evidence: ordinary real
domains (`caresly.com`, `t_users.com`, `swiftactivation.net`, `tontransport.ga`,
`hw.ac.uk` — a genuine university domain hosting a Wells Fargo phish), no brand claim, no
domain anomaly, and either passing authentication or carrying no authentication headers.
Every rule runs and correctly returns `Pass`. No scoring change can fix this, because
there is nothing to find.

**A real bug was found in the same measurement, and it was separate.** The first scorer
run reported 11/35 green. Four of those were **green *despite* an explicit `Unknown`
finding** — `Scorer.Colour` consulted unknowns only on the zero-flag path, so any
weight-1 finding bypassed the fail-safe. This is exactly the D-004 promise broken by an
implementation detail. Fixed, pinned by regression test
`OneWeightOneFindingPlusUnknown_IsNotGreen`, and those four samples now show grey.

**Distinguishing the two was the point.** The 11 conflated an implementation bug (4,
fixable) with an analytical limit (7, not fixable). Reporting them as one number would
have hidden a genuine fail-safe breach behind an unavoidable ceiling.

**Binding consequence for Phase 3:** the green state must be presented as a neutral
**"no issues found"** — never a tick, shield or checkmark. A tick asserts a safety
guarantee the engine cannot make. This is a UI constraint, not a wording preference.

**Lesson:** an integrity criterion asserted *before* measurement is worth writing down,
because the measurement is what reveals it was aspirational. The value was in stating it
up front and then revising it honestly, rather than quietly softening the target later.

---

## D-012 — Rule weights tuned from per-sample simulation, not aggregate arithmetic

**Decided:** Before changing a rule weight to influence a success criterion, simulate the
change in Python against the **actual per-sample findings** and confirm the effect. Do not
reason from aggregate counts.

**Why:** U1 needed 18/35 phishing samples at amber or red and stood at 15. Working from
aggregate numbers, the obvious move was to raise `identity-mismatch`'s Return-Path weight
from 1 to 2. Simulating it showed the advice was **wrong in both directions**:

| Scenario | U1 | P3 (legitimate amber/red) |
|---|---|---|
| Raise `identity-mismatch` 1→2 | 16/35 = 46% — **still short** | 1/35 = 3% — **loses the only clean FP record** |
| Raise `lookalike-domain` 1→2 | **18/35 = 51% — clears U1** | **0/35 = 0% — unaffected** |

The aggregate reasoning failed because it counted flags without knowing which rule produced
them. Four of the seven green samples were fail-safe breaches already showing grey (from
D-011), not sub-amber flags, and the only genuinely sub-amber sample was a
`lookalike-domain` one. Aggregate counts cannot distinguish those cases; per-sample data
can.

**Applied:** `lookalike-domain` signals 2 and 3, weight 1 → 2. Re-measurement matched the
simulation exactly (U1 51%, false green 17%, P3 0%).

**Cost of the method:** one throwaway Python script per weight question. **Cost of not
using it:** a rule change that would have shipped, failed to meet the criterion, and spent
the project's only legitimate-sample precision margin on nothing.

**Generalisation:** this is the same lesson as D-009 (rule 4) and D-010 (edit distance) —
a plausible-looking change is worth ten minutes of measurement before it is worth a line
of code. Three for three so far.