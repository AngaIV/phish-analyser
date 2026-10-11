# Backlog

Ordered work queue. Phase-level detail lives in the build plan; this file tracks the
concrete next actions and the smaller items that would otherwise be forgotten.

Last updated: 2026-10-11.

## Now — Phase 2 rules

Order follows the plan: easiest and highest value first. Each rule runs the same
six-step routine (spec → failing tests → implement → measure on dev → investigate
false positives → record precision).

1. **Rule 3 — `display-name-spoof`.** An email address or known brand name inside the
   display name while the actual address is unrelated (`"PayPal Service" <x@evil.ru>`).
2. **Rule 4 — `message-id-alignment`.** `Message-ID` domain and DKIM signing domain
   should align with the `From` domain.
3. **Rule 5 — `lookalike-domain`.** Homoglyphs, mixed scripts, edit distance against a
   brand list, suspicious TLDs, hyphen tricks. **Priority: the measurements say this is
   where detection has to come from** (`auth-results` recall is 0.17).
4. **Rule 6 — `received-chain`.** Hop count, timestamp order, private and reserved IPs.
5. **Rule 7 — `structural-oddities`.** Missing required headers, duplicated headers,
   unusual `X-Mailer`, malformed fields.
6. **Rule 8 — `authserv-trust`.** Only trust `Authentication-Results` written by our own
   receiving server. Closes the forged-header gap in rule 1.
7. **Scoring module.** Bubble number = count of flagged findings; bubble colour from the
   weighted score (green none, amber low/medium, red high). Move weights and brand lists
   to configuration (D-005).
8. **Holdout run.** Once, at the end of Phase 2, recorded and never tuned against.

## Next — Phase 1 leftovers

- **Light fuzzing (task 8).** Deliberately deferred until the parser surface freezes
  before the TypeScript port (D-008). Capped local run (~20 samples × 50 mutations),
  out of the per-PR CI run.
- **`evaluate.py` hardening.** Have it build the CLI itself rather than relying on a
  manual `dotnet build` (the D-007 stale-measurement trap).
- **`evaluate.py` tidy.** Remove the duplicated `n_phish` computation — the first
  assignment is dead.

## Soon — repository and process

- **`main` branch protection ruleset:** require a pull request, require the three CI
  checks (`dotnet`, `typescript`, `python`), block force-pushes.
- **Dependabot alerts and version updates.**
- **Secret scanning and push protection.**
- **CI on the open PR:** confirm all three jobs are green on `phase1/corpus-split`
  before merging (all three are green on `main` at `4fbf328`).
- **PR description** for `phase1/corpus-split` should mention both corpus growth and the
  split, since the branch name only reflects the latter.

## Later — documentation still to write

`docs/architecture.md`, `docs/success.md` and `docs/risk-register.md` are created but
empty. `docs/success.md` should be written **before** Phase 2 tuning finishes, since it
holds the false-positive threshold the Phase 2 exit gate is measured against.

## Known issues (documented, not fixed)

| Issue | Where | Disposition |
|---|---|---|
| `ham_007_world_wide_words.eml` false-positive from `identity-mismatch` | `docs/results.md` | A newsletter with no list headers; not fixable with header-only signals |
| Rule 2 recall fell 0.23 → 0.11 after the FP fix | `spec/rules/identity-mismatch.md` | Deliberate trade; revisit once rules 3 and 5 carry detection |
| `auth-results` trusts the first header, so a forged `pass` could win | `spec/rules/auth-results.md` | Rule 8; behaviour pinned by test |
| POPIA/ethics compliance gap in the plan's privacy section | Build plan | Raise with the I&TS stakeholder |
| Two weak auto-generated names (`ham_016_online`, `ham_020_ucd`) | `testdata/legitimate/` | Cosmetic only |