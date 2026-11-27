"""Refresh the offline page list and content hash after the final language/site-bar pass."""
import hashlib
import json
import re
from pathlib import Path

DOCS = Path(__file__).resolve().parents[1] / "docs"
page = DOCS / "sw.js"
source = page.read_text(encoding="utf-8")
match = re.search(r'CORE = (\[.*?\]), EXTRA = (\[.*?\]);', source)
if not match:
    raise SystemExit("sw.js: missing offline file lists")
core, extra = (json.loads(group) for group in match.groups())
extra.extend("/" + name for name in ("adeeboshell.html", "adeebolab-guide.html"))
extra.append("/media/wifi-or-provider-poster.jpg")  # the small local poster, not the full MP4
extra.append("/blog-articles.css")
extra.extend("/blog/" + p.name for p in sorted((DOCS / "blog").glob("*.html")) if p.name != "index.html")
for language in ("bn", "fr", "es", "ar"):
    extra.extend("/" + p.relative_to(DOCS).as_posix() for p in sorted((DOCS / language).rglob("*.html")))
extra = list(dict.fromkeys(path for path in extra if path not in core))
version = hashlib.sha256(b"".join((DOCS / path.lstrip("/")).read_bytes() for path in core + extra)).hexdigest()[:12]
source = re.sub(r'const V = "am-[a-f0-9]+"', 'const V = "am-' + version + '"', source)
source = re.sub(r'CORE = (\[.*?\]), EXTRA = (\[.*?\]);',
                lambda _: "CORE = " + json.dumps(core) + ", EXTRA = " + json.dumps(extra) + ";", source)
page.write_text(source, encoding="utf-8", newline="\n")
print(f"Final offline cache: {version}, {len(core) + len(extra)} files")
