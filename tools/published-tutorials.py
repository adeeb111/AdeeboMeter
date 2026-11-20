"""Connect published tutorials to the matching lessons after localization."""
import html
import json
import re
from pathlib import Path

DOCS = Path(__file__).resolve().parents[1] / "docs"
YOUTUBE = "https://www.youtube.com/watch?v=ebIGcO6hWmo"
FACEBOOK = "https://www.facebook.com/reel/1911120453369179"
TITLE = "What Is DNS? Follow a Real Lookup on Windows"
COPY = {
    "en": ("Watch the DNS tutorial", "Follow the query, resolver and reply, then run a real Windows check.", "English audio · 3-minute tutorial", "Watch on YouTube", "Watch on Facebook"),
    "bn": ("DNS-এর ভিডিও নির্দেশনা দেখুন", "DNS প্রশ্ন, রিজলভার ও উত্তর কীভাবে কাজ করে দেখুন, তারপর Windows-এ পরীক্ষা করুন।", "ইংরেজি অডিও · প্রায় ৩ মিনিট", "YouTube-এ দেখুন", "Facebook-এ দেখুন"),
    "fr": ("Regardez le tutoriel DNS", "Suivez la requête, le résolveur et la réponse, puis faites une vérification sous Windows.", "Audio en anglais · tutoriel de 3 minutes", "Regarder sur YouTube", "Regarder sur Facebook"),
    "es": ("Mira el tutorial de DNS", "Sigue la consulta, el resolvedor y la respuesta, y después haz una comprobación real en Windows.", "Audio en inglés · tutorial de 3 minutos", "Ver en YouTube", "Ver en Facebook"),
}


def install():
    count = 0
    for language, (heading, summary, audio, youtube, facebook) in COPY.items():
        page = DOCS / ("" if language == "en" else language) / "learn/dns.html"
        if not page.exists():
            continue
        content = page.read_text(encoding="utf-8-sig")
        original = content
        content = re.sub(r"<!--dns-video:start-->.*?<!--dns-video:end-->\s*", "", content, flags=re.S)
        escape = html.escape
        card = ('<!--dns-video:start--><section class="card" id="dns-video-tutorial" aria-labelledby="dns-video-heading" style="border-left:4px solid var(--c-learn,#2673e8)">'
                f'<h2 id="dns-video-heading">{escape(heading)}</h2>'
                f'<p lang="en">{TITLE}</p><p>{escape(summary)}</p><p class="tag">{escape(audio)}</p>'
                f'<p class="ui-ctas"><a class="cta" href="{YOUTUBE}">{escape(youtube)}</a>'
                f'<a class="cta ghost" href="{FACEBOOK}">{escape(facebook)}</a></p></section><!--dns-video:end-->\n')
        content, inserted = re.subn(r'(<div class="card one">.*?</div>\s*)', lambda match: match[1] + card, content, count=1, flags=re.S)
        if not inserted:
            raise ValueError(f"Missing lesson introduction: {page}")

        def link_tutorial(match):
            data = json.loads(match[1])
            if data.get("@type") == "LearningResource":
                # This remains a text lesson linking to external watch pages.
                # Do not claim that the lesson is a video watch page for rich results.
                data["subjectOf"] = {
                    "@type": "CreativeWork", "name": TITLE, "genre": "Video tutorial",
                    "url": YOUTUBE, "inLanguage": "en", "datePublished": "2026-11-19",
                    "publisher": {"@type": "Organization", "name": "AdeeboMeter"},
                }
            return '<script type="application/ld+json">' + json.dumps(data, ensure_ascii=False, separators=(",", ":")) + '</script>'

        content = re.sub(r'<script type="application/ld\+json">(.*?)</script>', link_tutorial, content, flags=re.S)
        if content != original:
            page.write_text(content, encoding="utf-8", newline="\n")
        count += 1
    print(f"Published DNS tutorial linked on {count} lesson pages.")


if __name__ == "__main__":
    install()
