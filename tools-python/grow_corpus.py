"""Grow the corpus to 100 labelled samples (50 phishing / 50 legitimate).

Deterministic: same seed, same picks. Skips anything already in labels.csv.
Sanitises to headers-only and derives factual notes from real header values.
"""
import csv
import email
import random
import re
from pathlib import Path

SEED = 20261011
ROOT = Path(__file__).resolve().parents[1]
TESTDATA = ROOT / "testdata"
PHISH_POOL = TESTDATA / "raw" / "phishing" / "split"
HAM_POOL_DIRS = ["easy_ham", "easy_ham_2", "hard_ham"]
SA = TESTDATA / "raw" / "spamassassin"

TARGET_PHISH = 50
TARGET_HAM = 50
TARGET_SIZE = PHISH_POOL  # placeholder; sizes enforced below


def headers_only(raw: bytes) -> bytes:
    text = raw.decode("utf-8", "ignore")
    m = re.search(r"\r?\n\r?\n", text)
    return (text[: m.start()] if m else text).encode("utf-8")


def parse(raw: bytes):
    try:
        return email.message_from_bytes(raw)
    except Exception:
        return None


def phish_note(msg, raw: bytes) -> str:
    """Factual, header-derived description of why this is phishing-like."""
    bits = []
    fr = msg.get("From", "")
    dom = re.search(r"@([A-Za-z0-9.-]+)", fr)
    ar = msg.get("Authentication-Results", "") or ""
    low = ar.lower()
    if "spf=fail" in low:
        bits.append("SPF fail")
    elif "spf=softfail" in low:
        bits.append("SPF softfail")
    elif "spf=none" in low:
        bits.append("SPF none")
    if "dkim=fail" in low:
        bits.append("DKIM fail")
    if "dmarc=fail" in low:
        bits.append("DMARC fail")
    elif "dmarc=none" in low:
        bits.append("DMARC none")
    if not msg.get("DKIM-Signature"):
        bits.append("no DKIM signature")
    if not msg.get("List-Unsubscribe"):
        bits.append("no List-Unsubscribe")
    if re.search(r"(?i)(paypal|netflix|dhl|fedex|ups|usps|apple|icloud|amazon|microsoft|office|onedrive|docusign|irs|bank|wells|chase|dropbox|wetransfer|meta|coinbase|wallet|adobe|linkedin)",
                 fr[:80]):
        bits.append("brand name in display name")
    if dom:
        host = dom.group(1).lower()
        if re.search(r"(?:-|\.)(?:com|net|org|co|info|site|live|xyz|top|club|online|support|help)\.", host) or \
           host.count("-") >= 2 or re.search(r"\d{4,}", host):
            bits.append("lookalike-looking sender domain")
    return "; ".join(bits) if bits else "phishing sample"


def ham_note(msg, raw: bytes) -> str:
    bits = []
    if msg.get("List-Unsubscribe"):
        bits.append("has List-Unsubscribe")
    if msg.get("List-Id"):
        bits.append("mailing-list headers")
    if re.search(r"(?im)^Precedence:\s*bulk", raw.decode("utf-8", "ignore")):
        bits.append("Precedence: bulk")
    if msg.is_multipart() and any(p.get_content_type() == "message/rfc822" for p in msg.walk()):
        bits.append("forwarded message")
    subj = (msg.get("Subject") or "")
    if re.match(r"(?i)^\s*(fwd|fw)\s*:", subj):
        bits.append("forwarded subject")
    if msg.get("In-Reply-To"):
        bits.append("thread reply")
    return "; ".join(bits) if bits else "ordinary legitimate mail"


def main() -> None:
    with open(TESTDATA / "labels.csv", newline="", encoding="utf-8") as fh:
        existing = list(csv.DictReader(fh))
    used = {r["file"] for r in existing}
    phish_have = sum(1 for r in existing if r["label"] == "phishing")
    ham_have = sum(1 for r in existing if r["label"] == "legitimate")

    rng = random.Random(SEED)
    rows = list(existing)
    next_p = phish_have + 1
    next_h = ham_have + 1

    # ---- phishing ----
    pool = sorted(PHISH_POOL.glob("msg_*.eml"))
    # skip ones already used by matching content length is unreliable; use index from existing names
    used_idx = set()
    for f in pool:
        raw = f.read_bytes()
        for r in existing:
            if r["label"] != "phishing":
                continue
            tgt = TESTDATA / "phishing" / r["file"]
            if tgt.exists() and tgt.read_bytes() == headers_only(raw):
                used_idx.add(f.name)
    pool = [f for f in pool if f.name not in used_idx]
    rng.shuffle(pool)

    added_p = 0
    for f in pool:
        if phish_have + added_p >= TARGET_PHISH:
            break
        raw = f.read_bytes()
        msg = parse(raw)
        if msg is None or not msg.get("From"):
            continue
        name = f"phish_{next_p:03d}_{f.name.replace('msg_', 'src')}.eml"
        (TESTDATA / "phishing" / name).write_bytes(headers_only(raw))
        rows.append({"file": name, "label": "phishing",
                     "source": "nazario-phishing-2023", "confidence": "high",
                     "notes": phish_note(msg, raw)})
        next_p += 1
        added_p += 1

    # ---- legitimate ----
    hampool = []
    for d in HAM_POOL_DIRS:
        for f in sorted((SA / d).glob("*")):
            if f.is_file():
                hampool.append((d, f))
    used_ham = set()
    for d, f in hampool:
        raw = f.read_bytes()
        for r in existing:
            if r["label"] != "legitimate":
                continue
            tgt = TESTDATA / "legitimate" / r["file"]
            if tgt.exists() and tgt.read_bytes() == headers_only(raw):
                used_ham.add((d, f.name))
    hampool = [(d, f) for d, f in hampool if (d, f.name) not in used_ham]
    rng.shuffle(hampool)

    added_h = 0
    for d, f in hampool:
        if ham_have + added_h >= TARGET_HAM:
            break
        raw = f.read_bytes()
        msg = parse(raw)
        if msg is None or not msg.get("From"):
            continue
        name = f"ham_{next_h:03d}_{f.name.split('.')[0]}.eml"
        (TESTDATA / "legitimate" / name).write_bytes(headers_only(raw))
        rows.append({"file": name, "label": "legitimate",
                     "source": f"spamassassin-{d}", "confidence": "high",
                     "notes": ham_note(msg, raw)})
        next_h += 1
        added_h += 1

    rows.sort(key=lambda r: (r["label"], r["file"]))
    with open(TESTDATA / "labels.csv", "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=["file", "label", "source", "confidence", "notes"])
        w.writeheader()
        w.writerows(rows)

    print(f"phishing added: {added_p} (total {phish_have + added_p})")
    print(f"legitimate added: {added_h} (total {ham_have + added_h})")
    print(f"labels.csv rows: {len(rows)}")


if __name__ == "__main__":
    main()