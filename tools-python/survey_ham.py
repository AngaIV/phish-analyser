"""Survey SpamAssassin easy_ham_2 for From/Subject and canary flags."""
from pathlib import Path
import email

SRC = Path("testdata/raw/spamassassin/easy_ham_2")

for f in sorted(SRC.iterdir()):
    if not f.is_file():
        continue
    raw = f.read_bytes()
    try:
        msg = email.message_from_bytes(raw)
        hdrs = {h.lower(): v for h, v in (msg.items())}
        frm = msg.get("From", "").replace("\n", " ")[:45]
        subj = (msg.get("Subject") or "").replace("\n", " ")[:45]
        flags = []
        if msg.get_all("List-Unsubscribe"): flags.append("unsub")
        if "Precedence: bulk" in raw.decode("utf-8", "ignore").lower(): flags.append("bulk")
        if msg.get_all("List-Id"): flags.append("list")
        # forwarded: multipart with message/rfc822 part, or X-Forwarded headers
        if msg.is_multipart():
            for part in msg.walk():
                if part.get_content_type() == "message/rfc822":
                    flags.append("fwd")
                    break
        for h in ("X-Forwarded-For", "X-Original-To", "In-Reply-To", "Delivered-To"):
            if msg.get(h): flags.append(h.lower().replace("-", ""))
        fl = ",".join(flags) if flags else "-"
        print(f"{f.name} | {fl} | {frm} | {subj}")
    except Exception as e:
        print(f"{f.name} | ERROR {e}")