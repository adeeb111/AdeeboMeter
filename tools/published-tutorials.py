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
    "zh": ("观看 DNS 教程", "了解查询、解析器和应答，再运行真实的 Windows 检查。", "英语音频 · 3 分钟教程", "在 YouTube 观看", "在 Facebook 观看"),
    "en": ("Watch the DNS tutorial", "Follow the query, resolver and reply, then run a real Windows check.", "English audio · 3-minute tutorial", "Watch on YouTube", "Watch on Facebook"),
    "bn": ("DNS-এর ভিডিও নির্দেশনা দেখুন", "DNS প্রশ্ন, রিজলভার ও উত্তর কীভাবে কাজ করে দেখুন, তারপর Windows-এ পরীক্ষা করুন।", "ইংরেজি অডিও · প্রায় ৩ মিনিট", "YouTube-এ দেখুন", "Facebook-এ দেখুন"),
    "fr": ("Regardez le tutoriel DNS", "Suivez la requête, le résolveur et la réponse, puis faites une vérification sous Windows.", "Audio en anglais · tutoriel de 3 minutes", "Regarder sur YouTube", "Regarder sur Facebook"),
    "ar": ("شاهد شرح DNS", "تابع الطلب والمحلّل والرد، ثم أجرِ فحصًا حقيقيًا على Windows.", "الصوت بالإنجليزية · شرح مدته 3 دقائق", "شاهد على YouTube", "شاهد على Facebook"),
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
    install_mtr()
    install_wifi()


MTR_URL = "https://www.youtube.com/watch?v=5IEcPIedL2I"
MTR_TITLE = "How to Read MTR Results on Windows | Packet Loss Explained"
MTR_SHORT = "https://www.youtube.com/shorts/L8fN2w5TXWQ"
MTR_REEL = "https://www.facebook.com/reel/1128060556600411"
MTR_SHORT_TITLE = "One Hop Shows Packet Loss. Is It Really a Problem?"
SHORT_COPY = {"en": ("Watch the Short", "Watch the Facebook clip"), "fr": ("Voir le Short", "Voir le clip Facebook"), "es": ("Ver el Short", "Ver el vídeo en Facebook"), "ar": ("شاهد المقطع القصير", "شاهد مقطع Facebook"), "bn": ("শর্টস দেখুন", "Facebook-এ ভিডিও দেখুন")}
MTR_COPY = {
    "en": ("Read an MTR result", "Compare an intermediate hop with the destination before blaming packet loss on that hop.", "English audio · 2:22 · uses the built-in example", "Watch on YouTube"),
    "fr": ("Lire un résultat MTR", "Comparez un saut intermédiaire à la destination avant de lui attribuer une perte de paquets.", "Audio en anglais · 2:22 · utilise l’exemple intégré", "Regarder sur YouTube"),
    "es": ("Cómo leer un resultado MTR", "Compara un salto intermedio con el destino antes de atribuirle la pérdida de paquetes.", "Audio en inglés · 2:22 · usa el ejemplo integrado", "Ver en YouTube"),
    "ar": ("فهم نتيجة MTR", "قارن القفزة الوسيطة بالوجهة قبل نسبة فقد الحزم إلى تلك القفزة.", "الصوت بالإنجليزية · 2:22 · يستخدم المثال المدمج", "شاهد على YouTube"),
    "bn": ("MTR-এর ফল বুঝুন", "কোনো ধাপে প্যাকেট হারানোর সিদ্ধান্ত নেওয়ার আগে মাঝের ধাপটি গন্তব্যের সঙ্গে তুলনা করুন।", "ইংরেজি অডিও · ২:২২ · বিল্ট-ইন উদাহরণ ব্যবহার করা হয়েছে", "YouTube-এ দেখুন"),
}


def install_mtr():
    count = 0
    for language, (heading, summary, audio, watch) in MTR_COPY.items():
        for route in ("learn/icmp.html", "isp-report.html"):
            page = DOCS / ("" if language == "en" else language) / route
            if not page.exists():
                continue
            content = page.read_text(encoding="utf-8-sig")
            content = re.sub(r"<!--mtr-video:start-->.*?<!--mtr-video:end-->\s*", "", content, flags=re.S)
            card_class = "card" if route.startswith("learn/") else "example"
            card = (f'<!--mtr-video:start--><section class="{card_class}" id="mtr-video-tutorial" aria-labelledby="mtr-video-heading">'
                    f'<h2 id="mtr-video-heading">{html.escape(heading)}</h2>'
                    f'<p lang="en">{MTR_TITLE}</p><p>{html.escape(summary)}</p><p>{html.escape(audio)}</p>'
                    f'<p><a href="{MTR_URL}">{html.escape(watch)}</a></p>'
                    f'<p lang="en">{MTR_SHORT_TITLE}</p><p><a href="{MTR_SHORT}">{html.escape(SHORT_COPY[language][0])}</a> · '
                    f'<a href="{MTR_REEL}">{html.escape(SHORT_COPY[language][1])}</a></p></section><!--mtr-video:end-->\n')
            if route.startswith("learn/"):
                content, n = re.subn(r'(<div class="card one">.*?</div>\s*)', lambda m: m[1] + card, content, count=1, flags=re.S)
                if not n:
                    raise ValueError(f"Missing lesson introduction: {page}")
            else:
                content = content.replace('<h2 id="checks">', card + '<h2 id="checks">', 1)
                if 'id="mtr-video-tutorial"' not in content:
                    raise ValueError(f"Missing guide checks: {page}")
            work = {"@type": "CreativeWork", "name": MTR_TITLE, "genre": "Video tutorial", "url": MTR_URL,
                    "inLanguage": "en", "datePublished": "2026-11-25", "publisher": {"@type": "Organization", "name": "AdeeboMeter"}}
            short_work = {"@type": "CreativeWork", "name": MTR_SHORT_TITLE, "genre": "Short video", "url": MTR_SHORT,
                          "inLanguage": "en", "sameAs": MTR_REEL, "publisher": {"@type": "Organization", "name": "AdeeboMeter"}}
            linked = [False]
            def add_subject(match):
                data = json.loads(match[1])
                if data.get("@type") in ("LearningResource", "WebPage"):
                    subjects = data.get("subjectOf", [])
                    subjects = subjects if isinstance(subjects, list) else [subjects]
                    data["subjectOf"] = [x for x in subjects if x.get("url") not in (MTR_URL, MTR_SHORT)] + [work, short_work]
                    if data.get("@type") == "WebPage":
                        data["url"] = "https://adeebometer.com/" + ("" if language == "en" else language + "/") + route
                        data["inLanguage"] = "fr-CA" if language == "fr" else language
                    linked[0] = True
                return '<script type="application/ld+json">' + json.dumps(data, ensure_ascii=False, separators=(",", ":")) + '</script>'
            content = re.sub(r'<script type="application/ld\+json">(.*?)</script>', add_subject, content, flags=re.S)
            if not linked[0]:
                relative = ("" if language == "en" else language + "/") + route
                data = {"@context": "https://schema.org", "@type": "WebPage", "url": "https://adeebometer.com/" + relative,
                        "inLanguage": "fr-CA" if language == "fr" else language, "subjectOf": [work, short_work]}
                content = content.replace("</head>", '<script type="application/ld+json">' + json.dumps(data, ensure_ascii=False, separators=(",", ":")) + '</script>\n</head>')
            page.write_text(content, encoding="utf-8", newline="\n")
            count += 1
    print(f"Published MTR tutorial linked on {count} pages.")


WIFI_URL = "https://www.youtube.com/watch?v=soEsOAInV-U"
WIFI_TITLE = "Is It Your Wi-Fi or Your Internet Provider? Check on Windows"
WIFI_COPY = {
    "en": ("Watch the Wi-Fi and Ethernet comparison", "Compare the round trip to your router with the round trip to an internet server. A faster router response on Ethernet does not prove that your provider is at fault.", "English audio · 1:21", "Watch on YouTube"),
    "fr": ("Voir la comparaison Wi-Fi et Ethernet", "Comparez l’aller-retour vers votre routeur à celui vers un serveur Internet. Une réponse plus rapide du routeur sur Ethernet ne prouve pas que le fournisseur est en cause.", "Audio en anglais · 1:21", "Regarder sur YouTube"),
    "es": ("Ver la comparación entre Wi-Fi y Ethernet", "Compara el tiempo de ida y vuelta al router con el de un servidor de internet. Una respuesta más rápida del router por Ethernet no demuestra que el proveedor sea el culpable.", "Audio en inglés · 1:21", "Ver en YouTube"),
    "bn": ("Wi-Fi ও Ethernet-এর তুলনা দেখুন", "রাউটার ও ইন্টারনেট সার্ভারে যাওয়া-আসার সময় তুলনা করুন। Ethernet-এ রাউটারের উত্তর দ্রুত হলেই সেবাদাতার সমস্যা প্রমাণ হয় না।", "ইংরেজি অডিও · ১:২১", "YouTube-এ দেখুন"),
    "ar": ("شاهد المقارنة بين Wi-Fi وEthernet", "قارن زمن الذهاب والعودة إلى الموجّه بزمن الذهاب والعودة إلى خادم إنترنت. استجابة الموجّه الأسرع عبر Ethernet لا تثبت أن المزوّد هو سبب المشكلة.", "الصوت بالإنجليزية · 1:21", "شاهد على YouTube"),
}


def install_wifi():
    count = 0
    for language, (heading, summary, audio, watch) in WIFI_COPY.items():
        page = DOCS / ("" if language == "en" else language) / "wifi-or-isp.html"
        if not page.exists():
            continue
        source = page.read_text(encoding="utf-8-sig")
        source = re.sub(r"<!--wifi-video:start-->.*?<!--wifi-video:end-->\s*", "", source, flags=re.S)
        card = ('<!--wifi-video:start--><section class="example" id="wifi-video-tutorial" aria-labelledby="wifi-video-heading">'
                f'<h2 id="wifi-video-heading">{html.escape(heading)}</h2><p>{html.escape(summary)}</p>'
                f'<p>{html.escape(audio)}</p><p><a href="{WIFI_URL}">{html.escape(watch)}</a></p>'
                '</section><!--wifi-video:end-->\n')
        source = source.replace('<h2 id="checks">', card + '<h2 id="checks">', 1)
        if 'id="wifi-video-tutorial"' not in source:
            raise ValueError(f"Missing guide checks: {page}")
        source = re.sub(r'<script[^>]*id="wifi-tutorial-schema"[^>]*>.*?</script>\s*', "", source, flags=re.S)
        data = {"@context": "https://schema.org", "@type": "CreativeWork", "name": WIFI_TITLE,
                "genre": "Video tutorial", "url": WIFI_URL, "inLanguage": "en",
                "datePublished": "2026-11-25", "publisher": {"@type": "Organization", "name": "AdeeboMeter"}}
        source = source.replace("</head>", '<script type="application/ld+json" id="wifi-tutorial-schema">' + json.dumps(data, ensure_ascii=False, separators=(",", ":")) + '</script>\n</head>', 1)
        page.write_text(source, encoding="utf-8", newline="\n")
        count += 1
    print(f"Published Wi-Fi tutorial linked on {count} guides.")


if __name__ == "__main__":
    install()
