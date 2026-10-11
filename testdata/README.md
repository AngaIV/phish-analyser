# testdata

Labelled, sanitised corpus. **Headers only** — bodies, attachments, links and personal data are stripped.

## Sources and licences
- **Phishing:** Nazario phishing corpus, phishing-2023 archive, https://monkey.org/~jose/phishing/ — CC-BY-4.0 (attribution required). Note: 2023+ archives are NOT anonymised; samples are stored headers-only.
- **Legitimate:** Apache SpamAssassin public corpus, 20030228_easy_ham_2, https://spamassassin.apache.org/old/publiccorpus/ — public corpus.
- **Raw/unmodified originals:** kept out of git in `testdata/raw/` (git-ignored). See also the SpamAssassin spam_2 archive (unused, extras only).

## Layout
- `phishing/`, `legitimate/`: sanitised samples used by the engine and golden tests.
- `splits/`: development/holdout split (created by tools-python/split_corpus.py).
- `raw/`: git-ignored originals. Never commit anything from here.
- `labels.csv`: file,label,source,confidence,notes per sample.
