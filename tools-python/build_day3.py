"""Day 3: select, sanitise (headers-only), copy 30 samples, write labels.csv + README."""
import csv, email, re
from pathlib import Path

ROOT = Path(".")
PHISH_SRC = Path("testdata/raw/phishing/split")
HAM_SRC = Path("testdata/raw/spamassassin/easy_ham_2")
PHISH_DST = Path("testdata/phishing")
HAM_DST = Path("testdata/legitimate")

# (source_id, destination_name, notes)
PHISHING = [
    ("msg_00001.eml", "phish_001_netflix_renewal_scan.eml", "netflix lookalike display name + spoof domain"),
    ("msg_00003.eml", "phish_002_teams_file_share.eml", "brand impersonation: Microsoft Teams share"),
    ("msg_00004.eml", "phish_003_fastway_courier.eml", "SA courier spoof; lookalike info.fastway.co.za"),
    ("msg_00006.eml", "phish_004_icloud_billing.eml", "Apple iCloud billing impersonation"),
    ("msg_00008.eml", "phish_005_paypal_unusual_activity.eml", "PayPal credential phish; mismatch domain"),
    ("msg_00016.eml", "phish_006_onedrive_share.eml", "OneDrive document-share phish"),
    ("msg_00028.eml", "phish_007_irs_tax_review.eml", "government tax impersonation"),
    ("msg_00029.eml", "phish_008_nedbank_statement.eml", "SA bank statement phish"),
    ("msg_00115.eml", "phish_009_dhl_delivery.eml", "DHL parcel phish; generic service.com addr"),
    ("msg_00146.eml", "phish_010_wetransfer_invoice.eml", "WeTransfer invoice phish; has List-Unsubscribe"),
    ("msg_00227.eml", "phish_011_amazon_signin.eml", "Amazon unusual sign-in"),
    ("msg_00237.eml", "phish_012_usps_package.eml", "USPS package delivery phish"),
    ("msg_00292.eml", "phish_013_wellsfargo_alert.eml", "Wells Fargo banking alert"),
    ("msg_00318.eml", "phish_014_icann_validation.eml", "ICANN domain validation phish"),
    ("msg_00320.eml", "phish_015_ups_delivery.phish.eml", "UPS delivery suspension alert"),
]
HAM = [
    ("01006.38cc51343349102cc40a2f3648713dad", "ham_001_fwd_camram_hashcash.eml", "CANARY: forwarded message (fwd)"),
    ("01293.1bfec3fa6ca7c5fbc5bd7ffdfe2c2780", "ham_002_oreilly_conference.eml", "CANARY: newsletter via third-party platform"),
    ("01277.d7a43a4dd78dc466c8808f370ae2b2bb", "ham_003_rhn_errata_alert.eml", "CANARY: mailing-list distribution (Delivered-To)"),
    ("01345.c40d5798193a4a060ec9f3d2321e37e4", "ham_004_securityfocus_news.eml", "security newsletter (list)"),
    ("01317.7fc86413a091430c3104b041a6525131", "ham_005_fm_news.eml", "freshmeat newsletter (list, unsub)"),
    ("01295.1b31839d0a6ab3c696ab369b5b40c70f", "ham_006_useperl_headlines.eml", "use Perl daily headlines"),
    ("01323.8f994027cca214b6e0e7df2d443ff4cd", "ham_007_world_wide_words.eml", "World Wide Words newsletter"),
    ("01389.e4cfb234aace4e12b2d9453686c911c9", "ham_008_ntk_now.eml", "NTK now newsletter"),
    ("01292.483e487ce52ebe78815349060f8aff71", "ham_009_vulnwatch_php.eml", "security advisory list (VulnWatch)"),
    ("01349.22ea9f2c5d135c39d01f56c830b15e41", "ham_010_freebsd_advisory.eml", "FreeBSD security advisory (list)"),
    ("01303.80dd19a2b1d8496c48396b630179b00f", "ham_011_cron_sitescooper.eml", "system/automated cron mail"),
    ("00970.98d919e3b073097d7af97e18910e230b", "ham_012_uw_robot_mime.eml", "automated robot reply (MIME info)"),
    ("01274.0d083a2d3b30061efdc2cc73ee9e76e3", "ham_013_blogged_item.eml", "personal-ish blog notification"),
    ("01388.8bdd1ecbba6a6739b067fe88cd0fa281", "ham_014_hertz_deals.eml", "marketing/bulk-style deals email"),
    ("01294.7f208bf4ae152863fd40f25e2e121d49", "ham_015_securiteinfo.eml", "security news bulletin"),
]

def headers_only(raw: bytes) -> bytes:
    """Keep the header block only (everything before the first blank line)."""
    text = raw.decode("utf-8", "ignore")
    m = re.search(r"\r?\n\r?\n", text)
    return (text[: m.start()] if m else text).encode("utf-8")

def find_ham(sid: str) -> Path:
    for f in HAM_SRC.iterdir():
        if f.is_file() and f.name == sid:
            return f
    raise FileNotFoundError(f"ham source {sid} not found")

rows = []
for sid, dst, note in PHISHING:
    src = PHISH_SRC / sid
    data = headers_only(src.read_bytes())
    (PHISH_DST / dst).write_bytes(data)
    rows.append((dst, "phishing", "nazario-phishing-2023", "high", note))
    print(f"phishing: {dst} ({len(data)} bytes)")

for sid, dst, note in HAM:
    src = find_ham(sid)
    data = headers_only(src.read_bytes())
    (HAM_DST / dst).write_bytes(data)
    rows.append((dst, "legitimate", "spamassassin-easy_ham_2", "high", note))
    print(f"ham:     {dst} ({len(data)} bytes)")

with open("testdata/labels.csv", "w", newline="", encoding="utf-8") as fh:
    w = csv.writer(fh)
    w.writerow(["file", "label", "source", "confidence", "notes"])
    w.writerows(rows)

readme = """# testdata

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
"""
(Path("testdata/README.md")).write_text(readme, encoding="utf-8")
print(f"\nlabels.csv written: {len(rows)} rows")