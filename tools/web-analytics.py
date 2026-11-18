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
    count = 0
    for page in DOCS.rglob("*.html"):
        if page.name in EXCLUDED or page.parts[-2] == "fr":
            continue
        content = page.read_text(encoding="utf-8-sig")
        original = content
        if 'rel="canonical"' not in content or "</head>" not in content:
            continue
        content = content.replace(TAG + "\n", "").replace(TAG, "")
        if 'src="/locale.js"' not in content:
            content = content.replace("</head>", TAG + "\n</head>", 1)
        if content != original:
            page.write_text(content, encoding="utf-8", newline="\n")
        count += 1
    print(f"Analytics loader installed on {count} public pages.")

if __name__ == "__main__":
    install()
