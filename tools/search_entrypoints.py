"""Improve the two process lessons already appearing in search. Preserve the design.

English web copy lives here; translations use the shared website dictionary.
The Windows app's knowledge-base text and quiz behavior remain independent.
"""
import html
import json
import re
from pathlib import Path

DOCS = Path(__file__).resolve().parents[1] / "docs"
SITE = "https://adeebometer.com/"
REVIEWED = "2026-11-26"
ENTRIES = {
    "msmpeng": {
        "title": "What Is MsMpEng.exe? Defender CPU Usage Explained",
        "description": "MsMpEng.exe is Microsoft Defender's Antimalware Service Executable. Learn what it does and compare CPU and disk use when Windows feels slow.",
        "answer": "MsMpEng.exe is Microsoft Defender’s Antimalware Service Executable. It checks files for malware; scan activity can use CPU and disk.",
        "source": "https://learn.microsoft.com/en-us/defender-endpoint/troubleshoot-performance-issues",
        "source_label": "Microsoft’s Defender performance guide",
    },
    "svchost": {
        "title": "What Is svchost.exe? Service Host and CPU Usage Explained",
        "description": "svchost.exe hosts Windows services. Learn why several copies run and how to compare a busy instance's CPU and disk activity.",
        "answer": "svchost.exe is Windows Service Host. Each instance hosts one or more services. Several instances can be normal; identify the busy service before changing anything.",
        "source": "https://learn.microsoft.com/en-us/windows/win32/services/service-programs",
        "source_label": "How Windows hosts services",
    },
}
HEADING = "PC slow? Start with one busy process."
STEPS = ("Find the process using the most CPU.", "Compare its disk activity at the same time.",
         "Repeat when the PC feels normal. Keep the time and what you were doing.")
CTA = "Compare CPU, memory and disk on Windows"
HUB = "A Windows process using CPU?"


def install():
    raw = (DOCS / "locale-messages.js").read_text(encoding="utf-8-sig")
    messages = json.loads(raw[raw.index("{"):raw.rindex("}") + 1])
    dates = []
    for lang in ("en", "bn", "fr", "es", "ar"):
        prefix = "" if lang == "en" else lang + "/"
        def t(key):
            return key if lang == "en" else messages[lang][key]
        def e(key):
            return html.escape(t(key), quote=True)
        for slug, entry in ENTRIES.items():
            page = DOCS / prefix / "learn" / (slug + ".html")
            if not page.exists():
                continue
            original = page.read_text(encoding="utf-8-sig")
            source = re.sub(r"<title>.*?</title>", "<title>" + e(entry["title"]) + " | AdeeboMeter</title>", original, count=1, flags=re.S)
            source = re.sub(r'(<meta (?:name="description"|property="og:description") content=")[^"]*', lambda m: m[1] + e(entry["description"]), source)
            source = re.sub(r'(<meta property="og:title" content=")[^"]*', lambda m: m[1] + e(entry["title"]) + " | AdeeboMeter", source)
            source, count = re.subn(r'<div class="card one">.*?</div>', '<div class="card one"><p>' + e(entry["answer"]) + '</p></div>', source, count=1, flags=re.S)
            if count != 1:
                raise ValueError(f"Missing lesson answer: {page}")
            source = re.sub(r"<!--process-check:start-->.*?<!--process-check:end-->\s*", "", source, flags=re.S)
            monitor = "/" + prefix + "pc-monitoring.html"
            rows = "".join('<li>' + e(step) + '</li>' for step in STEPS)
            block = ('<!--process-check:start--><section class="card" id="process-next-check" aria-labelledby="process-check-heading">'
                     '<h2 id="process-check-heading">' + e(HEADING) + '</h2><ol>' + rows + '</ol>'
                     '<p><a href="' + monitor + '">' + e(CTA) + '</a></p>'
                     '<p><a href="' + entry["source"] + '">' + e(entry["source_label"]) + '</a></p>'
                     '</section><!--process-check:end-->\n')
            # Keep the quiz intact; offer a practical next step before the app links.
            source, count = re.subn(r'(<p class="ui-ctas">)', lambda m: block + m[1], source, count=1)
            if count != 1:
                raise ValueError(f"Missing lesson quiz: {page}")
            if source != original:
                page.write_text(source, encoding="utf-8", newline="\n")
            dates.append(SITE + prefix + "learn/" + slug + ".html")
        page = DOCS / prefix / "pc-monitoring.html"
        if page.exists():
            original = page.read_text(encoding="utf-8-sig")
            source = re.sub(r"<!--process-links:start-->.*?<!--process-links:end-->\s*", "", original, flags=re.S)
            links = "".join('<p><a href="/' + prefix + 'learn/' + slug + '.html">' + e(entry["title"]) + '</a></p>' for slug, entry in ENTRIES.items())
            block = '<!--process-links:start--><nav aria-labelledby="process-links-heading"><h2 id="process-links-heading">' + e(HUB) + '</h2>' + links + '</nav><!--process-links:end-->\n'
            source = source.replace('</main>', block + '</main>', 1)
            if 'id="process-links-heading"' not in source:
                raise ValueError(f"Missing PC guide end section: {page}")
            if source != original:
                page.write_text(source, encoding="utf-8", newline="\n")
            dates.append(SITE + prefix + "pc-monitoring.html")
    page = DOCS / "sitemap.xml"
    source = page.read_text(encoding="utf-8")
    for url in dates:
        source = re.sub(r"(<loc>" + re.escape(url) + r"</loc>)(?:<lastmod>[^<]+</lastmod>)?", lambda m: m[1] + "<lastmod>" + REVIEWED + "</lastmod>", source)
    page.write_text(source, encoding="utf-8", newline="\n")
    print(f"Search entry points: {len(dates)} pages reviewed")


if __name__ == "__main__":
    install()
