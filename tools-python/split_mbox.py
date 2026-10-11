"""Split an mbox file into individual .eml messages under a subfolder."""
import mailbox
from pathlib import Path

SRC = Path("testdata/raw/phishing/phishing-2023.mbox")
OUT = Path("testdata/raw/phishing/split")

OUT.mkdir(parents=True, exist_ok=True)
mbox = mailbox.mbox(str(SRC))
for i, msg in enumerate(mbox):
    (OUT / f"msg_{i:05d}.eml").write_bytes(msg.as_bytes())
print(f"split {len(mbox)} messages into {OUT}")