"""Annotate complete page families after all four language generators have finished."""
import re
from pathlib import Path

DOCS = Path(__file__).resolve().parents[1] / "docs"
SITE = "https://adeebometer.com/"
languages = ("en", "bn", "fr", "es", "ar")
count = 0
for translated in sorted((DOCS / "bn").rglob("*.html")):
    relative = translated.relative_to(DOCS / "bn")
    pages = {lang: DOCS / ("" if lang == "en" else lang) / relative for lang in languages}
    if not all(page.exists() for page in pages.values()):
        continue
    route = relative.as_posix()
    route = route[:-10] if route.endswith("index.html") else route
    alternates = [(lang, SITE + ("" if lang == "en" else lang + "/") + route) for lang in languages]
    alternates.insert(3, ("fr-CA", SITE + "fr/" + route))
    alternates.append(("x-default", SITE + route))
    tags = "".join(f'<link rel="alternate" hreflang="{lang}" href="{url}">\n' for lang, url in alternates)
    for page in pages.values():
        source = page.read_text(encoding="utf-8-sig")
        updated = re.sub(r' data-language-variants="[^"]*"', ' data-language-variants="en bn fr es ar"', source, count=1)
        updated = re.sub(r'<link rel="alternate" hreflang="(?:en|bn|fr(?:-CA)?|es|ar|x-default)"[^>]*>\s*', "", updated)
        updated = updated.replace("</head>", tags + "</head>", 1)
        if updated != source:
            page.write_text(updated, encoding="utf-8", newline="\n")
            count += 1
print(f"Final language annotations: {count} pages updated")
