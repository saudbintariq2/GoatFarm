# -*- coding: utf-8 -*-
from pathlib import Path

p = Path(__file__).resolve().parent.parent / "src/GoatFarm.Web/Views/Trading/Index.cshtml"
c = p.read_text(encoding="utf-8")
replacements = [
    ("\u00c2\u00b7", " · "),  # Â· -> middle dot
    ("â€¦", "..."),
    ("â€"", "-"),
    ("â€"", "-"),
    ("â†'", "->"),
    ("â‚¨", "Rs"),
    ("â¬‡ ", ""),
]
for old, new in replacements:
    c = c.replace(old, new)
# Remove common mojibake emoji prefixes (UTF-8 read as latin-1)
import re
c = re.sub(r"\u00f0[\u0178\u0153][\u201c\u201d\u0178\u008d\u2019\u2013]?\s*", "", c)
c = re.sub(r"ð[^\s<]{0,12}\s*", "", c)
p.write_text(c, encoding="utf-8", newline="\n")
print("Fixed Index.cshtml")
