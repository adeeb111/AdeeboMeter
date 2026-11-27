"""Keep the five authored English scenario articles linked and discoverable.

Article HTML is maintained source. Translation copies are added only after review.
"""
import html
import json
import re
from pathlib import Path

DOCS = Path(__file__).resolve().parents[1] / "docs"
SITE = "https://adeebometer.com/"
DATE = "2026-11-27"
POSTS = (
    ("fast-speed-test-buffering", ("Test", "Playback")),
    ("game-lag-or-low-fps", ("PC", "Network")),
    ("internet-slow-every-evening", ("Earlier", "Evening")),
    ("wifi-disconnects-ethernet-works", ("Wi-Fi", "Ethernet")),
    ("mtr-one-hop-packet-loss", ("Middle hop", "Destination")),
)
RELATED = {
    "streaming.html": ("fast-speed-test-buffering", "internet-slow-every-evening"),
    "gaming.html": ("game-lag-or-low-fps", "mtr-one-hop-packet-loss"),
    "wifi-or-isp.html": ("wifi-disconnects-ethernet-works", "internet-slow-every-evening"),
    "internet-speed-monitor.html": ("fast-speed-test-buffering", "internet-slow-every-evening"),
    "isp-report.html": ("mtr-one-hop-packet-loss", "internet-slow-every-evening"),
}


def strip(source):
    return re.sub(r"<!--english-scenarios:start-->.*?<!--english-scenarios:end-->\s*", "", source, flags=re.S)


def install():
    items = {}
    cards = []
    for slug, labels in POSTS:
        page = DOCS / "blog" / (slug + ".html")
        source = page.read_text(encoding="utf-8")
        title = html.unescape(re.search(r"<h1>(.*?)</h1>", source, re.S)[1])
        description = html.unescape(re.search(r'<meta name="description" content="([^"]+)"', source)[1])
        route = "/blog/" + slug + ".html"
        items[slug] = (title, route)
        visual = '<ol class="path" aria-label="What this guide compares">' + ''.join('<li class="' + kind + '">' + html.escape(label) + '</li>' for kind, label in zip(("ok", "warn"), labels)) + '</ol>'
        cards.append('<li><a href="' + route + '"><div><h2>' + html.escape(title) + '</h2><p>' + html.escape(description) + '</p><ul class="meta"><li>3 min read</li></ul></div>' + visual + '</a></li>')
    page = DOCS / "blog" / "index.html"
    source = strip(page.read_text(encoding="utf-8"))
    block = '<!--english-scenarios:start-->' + '\n'.join(cards) + '<!--english-scenarios:end-->\n'
    source = source.replace('<ol class="posts">', '<ol class="posts">\n' + block, 1)
    if source.count('<!--english-scenarios:start-->') != 1:
        raise ValueError("Missing blog list")
    source = re.sub(r'<script[^>]*id="scenario-blog-list"[^>]*>.*?</script>\s*', '', source, flags=re.S)
    rows = re.findall(r'<li><a href="([^"]+)"><div><h2>(.*?)</h2>', source, re.S)
    data = {"@context": "https://schema.org", "@type": "ItemList", "name": "AdeeboMeter troubleshooting guides",
            "itemListElement": [{"@type": "ListItem", "position": i + 1, "url": SITE.rstrip('/') + route, "name": html.unescape(title)} for i, (route, title) in enumerate(rows)]}
    source = source.replace('</head>', '<script type="application/ld+json" id="scenario-blog-list">' + json.dumps(data, ensure_ascii=False, separators=(',', ':')) + '</script>\n</head>', 1)
    page.write_text(source, encoding="utf-8", newline="\n")
    for route, slugs in RELATED.items():
        page = DOCS / route
        source = strip(page.read_text(encoding="utf-8"))
        links = ''.join('<p><a href="' + items[slug][1] + '">' + html.escape(items[slug][0]) + '</a></p>' for slug in slugs)
        block = '<!--english-scenarios:start--><nav aria-labelledby="scenario-guides-heading"><h2 id="scenario-guides-heading">Try a real scenario</h2>' + links + '</nav><!--english-scenarios:end-->\n'
        if route == "wifi-or-isp.html":
            block = block.replace('<!--english-scenarios:start-->', '<!--english-scenarios:start-->' + "<section class=\"example\" id=\"connected-no-internet\" aria-labelledby=\"connected-no-internet-heading\"><h2 id=\"connected-no-internet-heading\">Wi-Fi connected, but nothing loads?</h2><p>Check three stops: your router, a destination beyond it, then DNS name lookups. In AdeeboMeter, open Home → Today → I have no internet.</p><p>English audio · about 1 minute. In this demo the connection was up and the PC had high CPU usage; your results may differ.</p><p><a href=\"https://www.youtube.com/watch?v=bp-w00rHs60\">Watch: Wi-Fi Connected but No Internet? 3 Checks on Windows</a> · <a href=\"/learn/dns.html\">Understand DNS</a></p></section>")
        source = source.replace('</main>', block + '</main>', 1)
        page.write_text(source, encoding="utf-8", newline="\n")
    page = DOCS / "sitemap.xml"
    source = page.read_text(encoding="utf-8")
    for route in ["blog/"] + ["blog/" + slug + ".html" for slug, _ in POSTS]:
        url = SITE + route
        if '<loc>' + url + '</loc>' not in source:
            source = source.replace('</urlset>', '  <url><loc>' + url + '</loc><lastmod>' + DATE + '</lastmod></url>\n</urlset>')
        else:
            source = re.sub(r'(<loc>' + re.escape(url) + r'</loc>)(?:<lastmod>[^<]+</lastmod>)?', lambda m: m[1] + '<lastmod>' + DATE + '</lastmod>', source)
    page.write_text(source, encoding="utf-8", newline="\n")
    print(f"Scenario blog: {len(POSTS)} articles, {len(rows)} blog cards")


if __name__ == "__main__":
    install()
