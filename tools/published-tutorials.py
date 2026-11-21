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
DIG_COPY = {
    "en": ("Next: read a DNS answer with dig", "Find the returned address and tell a name error from a timeout.", "English audio · under 2 minutes", "Watch on YouTube", "Watch on Facebook"),
    "bn": ("এবার dig দিয়ে DNS-এর উত্তর পড়ুন", "ফিরে আসা ঠিকানা খুঁজুন এবং নামের ত্রুটি ও টাইমআউটের পার্থক্য বুঝুন।", "ইংরেজি অডিও · ২ মিনিটের কম", "YouTube-এ দেখুন", "Facebook-এ দেখুন"),
    "fr": ("Ensuite : lire une réponse DNS avec dig", "Repérez l’adresse retournée et distinguez une erreur de nom d’un délai d’attente dépassé.", "Audio en anglais · moins de 2 minutes", "Regarder sur YouTube", "Regarder sur Facebook"),
    "es": ("Después: lee una respuesta DNS con dig", "Encuentra la dirección devuelta y distingue un error de nombre de un tiempo de espera agotado.", "Audio en inglés · menos de 2 minutos", "Ver en YouTube", "Ver en Facebook"),
}
TUTORIALS = (
    {"id": "dns", "youtube": YOUTUBE, "facebook": FACEBOOK, "title": TITLE, "published": "2026-11-19", "copy": COPY},
    {"id": "dig", "youtube": "https://www.youtube.com/watch?v=OEMDx3D-UZA", "facebook": "https://www.facebook.com/reel/1161786263048511", "title": "dig Command Explained: How to Read a DNS Answer", "published": "2026-11-20", "copy": DIG_COPY},
)


def install():
    count = 0
    for language in COPY:
        page = DOCS / ("" if language == "en" else language) / "learn/dns.html"
        if not page.exists():
            continue
        content = page.read_text(encoding="utf-8-sig")
        original = content
        escape = html.escape
        cards = []
        for tutorial in TUTORIALS:
            ident = tutorial["id"]
            heading, summary, audio, youtube, facebook = tutorial["copy"][language]
            content = re.sub(rf"<!--{ident}-video:start-->.*?<!--{ident}-video:end-->\s*", "", content, flags=re.S)
            cards.append(f'<!--{ident}-video:start--><section class="card" id="{ident}-video-tutorial" aria-labelledby="{ident}-video-heading" style="border-left:4px solid var(--c-learn,#2673e8)">'
                         f'<h2 id="{ident}-video-heading">{escape(heading)}</h2>'
                         f'<p lang="en">{escape(tutorial["title"])}</p><p>{escape(summary)}</p><p class="tag">{escape(audio)}</p>'
                         f'<p class="ui-ctas"><a class="cta" href="{tutorial["youtube"]}">{escape(youtube)}</a>'
                         f'<a class="cta ghost" href="{tutorial["facebook"]}">{escape(facebook)}</a></p></section><!--{ident}-video:end-->\n')
        content, inserted = re.subn(r'(<div class="card one">.*?</div>\s*)', lambda match: match[1] + "".join(cards), content, count=1, flags=re.S)
        if not inserted:
            raise ValueError(f"Missing lesson introduction: {page}")

        def link_tutorial(match):
            data = json.loads(match[1])
            if data.get("@type") == "LearningResource":
                # This remains a text lesson linking to external watch pages.
                # Do not claim that the lesson is a video watch page for rich results.
                data["subjectOf"] = [{
                    "@type": "CreativeWork", "name": tutorial["title"], "genre": "Video tutorial",
                    "url": tutorial["youtube"], "inLanguage": "en", "datePublished": tutorial["published"],
                    "publisher": {"@type": "Organization", "name": "AdeeboMeter"},
                } for tutorial in TUTORIALS]
            return '<script type="application/ld+json">' + json.dumps(data, ensure_ascii=False, separators=(",", ":")) + '</script>'

        content = re.sub(r'<script type="application/ld\+json">(.*?)</script>', link_tutorial, content, flags=re.S)
        if content != original:
            page.write_text(content, encoding="utf-8", newline="\n")
        count += 1
    print(f"Published {len(TUTORIALS)} tutorials on {count} DNS lesson pages.")


if __name__ == "__main__":
    install()
