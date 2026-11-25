"""Add the website analytics loader to public pages after site generation.

Set the public Cloudflare site token in docs/analytics.js before deployment.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
TAG = '<script defer src="/analytics.js"></script>'
# Companion tools can display private network details and sharing links.
EXCLUDED = {"app.html", "files.html", "live.html", "remote.html", "r.html", "games.html"}

def install():
    count = changed = viaLocale = 0; skipped = []
    for page in DOCS.rglob("*.html"):
        if page.name in EXCLUDED:
            continue
        content = page.read_text(encoding="utf-8-sig")
        original = content
        if 'rel="canonical"' not in content or "</head>" not in content:
            skipped.append(str(page.relative_to(DOCS)) if 'DOCS' in globals() else str(page)); continue   # R2-186: said, not silent
        content = content.replace(TAG + "\n", "").replace(TAG, "")
        if 'src="/locale.js"' not in content:
            content = content.replace("</head>", TAG + "\n</head>", 1)
        if content != original:
            page.write_text(content, encoding="utf-8", newline="\n"); changed += 1
        count += 1 if 'src="/locale.js"' not in content else 0
        viaLocale += 1 if 'src="/locale.js"' in content else 0
    print(f"Analytics loader tag on {count} pages ({changed} changed this run); {viaLocale} more load it through locale.js.")
    if skipped: print(f"Skipped (no canonical link or no </head>): {len(skipped)} - " + ", ".join(skipped[:8]))

if __name__ == "__main__":
    install()
    # Run after locale generation and before learnsite.py calculates the cache hash.
    import runpy
    runpy.run_path(str(ROOT / "tools/published-tutorials.py"), run_name="__main__")
