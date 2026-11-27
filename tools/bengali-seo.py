"""Generate crawlable localized pages from the reviewed website dictionaries.

Run after tools/learnsite.py. Uses only the Python standard library.
"""
import html
import json
import re
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urljoin, urlsplit

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
SITE = "https://adeebometer.com/"
raw = (DOCS / "locale-messages.js").read_text(encoding="utf-8-sig")
LANGUAGE = globals().get("LANGUAGE", "bn")
LANG_TAG = {"fr": "fr-CA", "bn": "bn", "es": "es", "ar": "ar", "zh": "zh-Hans"}[LANGUAGE]
messages = json.loads(raw[raw.index("{"):raw.rindex("}") + 1])[LANGUAGE]
bengali_links = {
    "To capture ping and traceroute results together,": "পিং ও ট্রেসরাউটের ফল একসঙ্গে সংগ্রহ করতে",
    "create a packet loss report for your internet provider": "ইন্টারনেট সেবাদাতার জন্য প্যাকেট লসের রিপোর্ট তৈরি করুন",
    "Compare memory use with CPU and disk activity using": "মেমরির ব্যবহার, CPU ও ডিস্কের কাজকর্ম একসঙ্গে দেখুন",
    "AdeeboMeter’s Windows PC monitor": "AdeeboMeter-এর Windows পিসি পর্যবেক্ষণে",
    "To see how both change while you work,": "কাজের সময় দুটির পরিবর্তন দেখতে",
    "monitor internet speed on Windows": "Windows-এ ইন্টারনেটের গতি পর্যবেক্ষণ করুন",
    "If the connection keeps dropping, compare Wi-Fi with Ethernet and": "সংযোগ বারবার বিচ্ছিন্ন হলে Wi-Fi ও Ethernet তুলনা করুন এবং",
    "save a connection report for your internet provider": "ইন্টারনেট সেবাদাতার জন্য সংযোগের রিপোর্ট রাখুন",
}
messages.update(bengali_links if LANGUAGE == "bn" else {
    "Disk": "Disque",
    "AdeeboMeter app and website · last updated 18 November 2026": "Application et site Web AdeeboMeter · mise à jour le 18 novembre 2026",
    "The Windows app": "L’application Windows",
    "collects nothing about you. There is no account, no telemetry, no analytics and no server. Everything it learns stays on your PC, and it never accepts a connection from anywhere.": "ne recueille aucune information sur vous. Aucun compte, aucune télémétrie, aucune analyse d’utilisation et aucun serveur. Les données restent sur votre PC et l’application n’accepte aucune connexion entrante.",
    "Public website pages use Cloudflare Web Analytics to measure page views, visits, referring websites and page performance. These totals help us understand which guides people find and improve the website. Cloudflare Web Analytics uses no analytics cookies or browser fingerprinting. A visit count is an estimate based on page requests and referrals.": "Les pages publiques utilisent Cloudflare Web Analytics pour mesurer les pages vues, les visites, les sites référents et la performance. Ces totaux nous aident à améliorer les guides et le site. Cloudflare Web Analytics n’utilise aucun témoin d’analyse ni empreinte numérique du navigateur. Le nombre de visites est une estimation fondée sur les pages consultées et les sites référents.",
    "The analytics script loads from": "Le script d’analyse est chargé depuis",
    "and sends measurements to Cloudflare. Cloudflare receives the network request as the service provider. Read": "et transmet les mesures à Cloudflare. Cloudflare reçoit la requête réseau à titre de fournisseur du service. Consultez",
    "Cloudflare's data collection documentation": "la documentation de Cloudflare sur la collecte des données",
    ". The website respects Do Not Track and Global Privacy Control by skipping analytics when either signal is enabled.": ". Le site respecte Do Not Track et Global Privacy Control en désactivant l’analyse lorsque l’un de ces signaux est activé.",
    "The website also uses two other external services: the home page asks": "Le site utilise aussi deux autres services externes. La page d’accueil interroge",
    "v2c and v3": "v2c et v3",
    "To capture ping and traceroute results together,": "Pour recueillir ensemble les résultats de ping et de traceroute,",
    "create a packet loss report for your internet provider": "créez un rapport de perte de paquets pour votre fournisseur Internet",
    "Compare memory use with CPU and disk activity using": "Comparez la mémoire, le processeur et le disque avec",
    "AdeeboMeter’s Windows PC monitor": "la surveillance du PC Windows d’AdeeboMeter",
    "To see how both change while you work,": "Pour suivre leur évolution pendant votre travail,",
    "monitor internet speed on Windows": "surveillez le débit Internet sous Windows",
    "If the connection keeps dropping, compare Wi-Fi with Ethernet and": "Si la connexion coupe souvent, comparez le Wi-Fi et Ethernet, puis",
    "save a connection report for your internet provider": "enregistrez un rapport pour votre fournisseur Internet",
} if LANGUAGE == "fr" else {})
if LANGUAGE == "es":
    spanish = json.loads((ROOT / "tools/spanish-seo.json").read_text(encoding="utf-8"))
    messages.update(spanish["messages"])
french_sources = [DOCS / name for name in (
    "index.html", "blog/index.html", "wifi-or-isp.html", "internet-speed-monitor.html",
    "pc-monitoring.html", "isp-report.html", "privacy.html", "gaming.html",
    "streaming.html", "trading.html",
)] + sorted((DOCS / "learn").glob("*.html"))
files = sorted((DOCS / "learn").glob("*.html"))
files = french_sources   # 21 Nov: every language gets the same pages, made from the English ones (Bangla had its own hand-made home page and guides)
localized = {"/" + p.relative_to(DOCS).as_posix() for p in files}
localized.update(route[:-10] for route in list(localized) if route.endswith("index.html"))
localized.update({"/learn/", "/", "/blog/", "/wifi-or-isp.html",
                  "/internet-speed-monitor.html", "/pc-monitoring.html", "/isp-report.html"})

pending = {}
def stage(path, text): pending[Path(path)] = text
def staged(path): return pending.get(Path(path)) or Path(path).read_text(encoding="utf-8-sig")

def translate(text):
    key = text.strip()
    translated = messages.get(key)
    if translated is None:
        match = re.fullmatch(r"(← )?(.+?)( →| \| Learn \| AdeeboMeter)?", key)
        if match:
            prefix, title, suffix = match.groups()
            translated_title = messages.get(title, title)
            if suffix == " | Learn | AdeeboMeter":
                suffix = " | " + messages.get("Learn", {"bn": "শিখুন"}.get(LANGUAGE, "Learn")) + " | AdeeboMeter"   # R2-184 / R2-171: never Bengali in another language; the name stays AdeeboMeter
            translated = (prefix or "") + translated_title + (suffix or "")
    return text.replace(key, translated or key, 1) if key else text

def language_links(url):
    english = re.sub(r"/(?:bn|fr|es|ar|zh)/", "/", url, count=1)
    relative = english.removeprefix(SITE)
    file = relative + "index.html" if relative.endswith("/") or not relative else relative
    links = [("en", english)]
    if (DOCS / "bn" / file).exists():
        links.append(("bn", SITE + "bn/" + relative))
    if DOCS / file in french_sources:
        links.extend([("fr", SITE + "fr/" + relative), ("fr-CA", SITE + "fr/" + relative)])
    if DOCS / file in french_sources:
        links.append(("es", SITE + "es/" + relative))
    if (DOCS / "ar" / file).exists():
        links.append(("ar", SITE + "ar/" + relative))
    if DOCS / file in french_sources:
        links.append(("zh-Hans", SITE + "zh/" + relative))
    links.append(("x-default", english))   # R2-200: every page says where unmatched languages go - its English page
    return "".join(f'<link rel="alternate" hreflang="{code}" href="{href}">\n'
                   for code, href in links)


def route_languages(url):
    return " ".join(dict.fromkeys(code.split("-")[0]
                    for code in re.findall(r'hreflang="([^"]+)"', language_links(url))
                    if code != "x-default"))


class LocalizedPage(HTMLParser):
    def __init__(self, source_url):
        super().__init__(convert_charrefs=False)
        self.source_url = source_url
        self.output = []
        self.excluded = []
        self.in_title = False

    def handle_starttag(self, tag, attrs):
        data = dict(attrs)
        if tag in ("script", "style", "code", "pre") or (tag == "svg" and LANGUAGE == "bn"):
            self.excluded.append(tag)
        self.in_title = self.in_title or tag == "title"
        if tag == "html":
            data["lang"] = LANG_TAG
            if LANGUAGE == "ar":
                data["dir"] = "rtl"   # Arabic reads right to left; locale.js sets the same when the page opens
            data["data-language-route"] = urlsplit(self.source_url).path
            data["data-language-variants"] = route_languages(self.source_url)
        for attr in ("href", "src", "action"):
            value = data.get(attr, "")
            if not value or value.startswith(("#", "data:", "mailto:", "javascript:")):
                continue
            target = urlsplit(urljoin(self.source_url, value))
            if target.netloc != "adeebometer.com":
                continue
            route = target.path
            if attr == "href" and route in localized:
                route = "/" + LANGUAGE + route
            data[attr] = route + ("?" + target.query if target.query else "") + ("#" + target.fragment if target.fragment else "")
        if tag == "link" and data.get("rel") == "canonical":
            data["href"] = self.source_url.replace(SITE, SITE + LANGUAGE + "/")
        for attr in ("aria-label", "placeholder", "alt", "title"):
            if attr in data:
                data[attr] = translate(html.unescape(data[attr]))
        if tag == "meta" and (data.get("property") in ("og:title", "og:description", "og:image:alt") or data.get("name") in ("twitter:title", "twitter:description")):
            data["content"] = translate(data["content"])
        if tag == "meta" and data.get("property") == "og:url":
            data["content"] = self.source_url.replace(SITE, SITE + LANGUAGE + "/")
        if tag == "meta" and data.get("property") == "og:locale":
            data["content"] = {"fr": "fr_CA", "bn": "bn_BD", "es": "es_ES", "ar": "ar_AR", "zh": "zh_CN"}[LANGUAGE]
        rendered = "".join(" " + key + ('="' + html.escape(value, quote=True) + '"' if value is not None else "")
                           for key, value in data.items())
        self.output.append("<" + tag + rendered + ">")

    def handle_endtag(self, tag):
        if tag == "head":
            url = self.source_url.replace(SITE, SITE + LANGUAGE + "/")
            self.output.append(language_links(url))
        self.output.append("</" + tag + ">")
        if self.excluded and self.excluded[-1] == tag:
            self.excluded.pop()
        if tag == "title":
            self.in_title = False

    def handle_data(self, data):
        self.output.append(data if self.excluded else html.escape(translate(html.unescape(data)), quote=False))

    def handle_entityref(self, name):
        self.output.append("&" + name + ";")

    def handle_charref(self, name):
        self.output.append("&#" + name + ";")

    def handle_comment(self, data):
        self.output.append("<!--" + data + "-->")

    def handle_decl(self, decl):
        self.output.append("<!" + decl + ">")

selected_pages = globals().get("PAGE_FILTER")
if selected_pages:
    files = [source for source in files if source.relative_to(DOCS).as_posix() in selected_pages]
for source in files:
    relative = source.relative_to(DOCS).as_posix()
    url = SITE + (relative[:-10] if relative.endswith("index.html") else relative)
    parser = LocalizedPage(url)
    # Decode character references before text translation without altering markup.
    content = source.read_text(encoding="utf-8-sig")
    content = re.sub(r'<link rel="alternate" hreflang="(?:en|bn|fr(?:-CA)?|es|ar|zh(?:-Hans)?|x-default)"[^>]*>\s*', "", content)
    # Only text between tags - never the code inside <script> or <style>: escaping it there turned "a > b" into
    # "a &gt; b" and broke every inline script on the translated pages (21 Nov).
    # (look ahead for the "<" rather than taking it, or a <script> right after text is never seen as one)
    content = re.sub(r"(<(script|style)\b[^>]*>.*?</\2>)|>([^<]+)(?=<)",
                     lambda m: m[1] if m[1] else ">" + html.escape(html.unescape(m[3]), quote=False), content, flags=re.S | re.I)
    # Parser assembles split entities into text by using normal conversion.
    parser.convert_charrefs = True
    parser.feed(content)
    rendered = "".join(parser.output)
    mt = re.search(r"<title>(.*?)</title>", rendered, re.S)
    if not mt: raise SystemExit("no <title> in " + str(source) + " - nothing was written")
    title = mt[1]
    paragraphs = re.findall(r"<p[^>]*>(.*?)</p>", rendered, re.S)
    paragraph = next((re.sub("<[^>]+>", "", p) for p in paragraphs if (LANGUAGE != "bn" or re.search("[অ-হ]", p))), "")
    description = html.escape(html.unescape(paragraph or title).strip()[:170], quote=True)
    rendered = re.sub(r'(<meta (?:name="description"|property="og:description") content=")([^"]*)',
                      lambda m: m[1] + (html.escape(translate(html.unescape(m[2])), quote=True) if LANGUAGE != "bn" and translate(html.unescape(m[2])) != html.unescape(m[2]) else description), rendered)
    if LANGUAGE in ("fr", "es", "ar", "zh") or relative == "index.html":
        descriptions = {
            "index.html": "Trouvez pourquoi votre Internet ou votre PC ralentit. Vérifiez le Wi-Fi, le routeur et le DNS. Gratuit, sans publicité ni droits d’administrateur.",
            "blog/index.html": "Guides visuels pour diagnostiquer un Internet lent, les coupures Wi-Fi, les pertes de paquets et un PC Windows qui ralentit.",
            "wifi-or-isp.html": "Comparez le Wi-Fi et Ethernet pour savoir si votre connexion ralentit à la maison ou plus loin, chez votre fournisseur Internet.",
            "internet-speed-monitor.html": "Surveillez le débit montant et descendant avec le délai de connexion sous Windows. Repérez ce qui change quand votre Internet ralentit.",
            "pc-monitoring.html": "Comparez le processeur, la mémoire, le disque et le réseau quand votre PC Windows ralentit. Trouvez la prochaine vérification à faire.",
            "isp-report.html": "Préparez un rapport de pertes de paquets pour votre fournisseur Internet. Comparez le routeur, la destination et les périodes avec ou sans problème.",
            "privacy.html": "Découvrez comment AdeeboMeter protège les données de l’application Windows et comment le site mesure les visites avec Cloudflare Web Analytics.",
            "gaming.html": "Votre jeu a du lag? Vérifiez le PC, le Wi-Fi et le trajet vers le serveur avec AdeeboMeter, un outil gratuit pour Windows.",
            "streaming.html": "Netflix, YouTube ou IPTV se met en mémoire tampon? Repérez les ralentissements du PC, du Wi-Fi, du fournisseur et du serveur de diffusion.",
            "trading.html": "Surveillez votre connexion au courtier avec AdeeboMeter pour Windows et repérez les ralentissements pendant les heures de marché.",
        }
        if LANGUAGE == "es":
            descriptions = spanish["descriptions"]
        if LANGUAGE == "ar":   # R2-104: the home and privacy pages carry a real sentence, not a stub
            descriptions = {"index.html": "اعرف لماذا أصبح الإنترنت أو الكمبيوتر بطيئًا. افحص الكمبيوتر وWi-Fi والراوتر وDNS واتصال الإنترنت على Windows. مجاني، بلا إعلانات، ولا يحتاج صلاحيات المسؤول.",
                            "privacy.html": "كيف يحمي AdeeboMeter بيانات تطبيق Windows، وكيف يقيس الموقع الزيارات عبر Cloudflare Web Analytics."}
        if LANGUAGE == "bn":
            descriptions = {"index.html": "আপনার ইন্টারনেট বা পিসি কেন ধীর, তা জানুন। Windows-এ পিসি, Wi-Fi, রাউটার ও ইন্টারনেট সংযোগ পরীক্ষা করুন। অ্যাডমিন অধিকার লাগে না।"}
        if LANGUAGE == "zh":
            descriptions = {"index.html": "查明网络或电脑变慢的原因。检查 Windows 电脑、Wi-Fi、路由器和 DNS。免费，无广告，无需管理员权限。"}
        if relative in descriptions:
            description = html.escape(descriptions[relative], quote=True)
            rendered = re.sub(r'(<meta (?:name="description"|property="og:description"|name="twitter:description") content=")[^"]*',
                              lambda m: m[1] + description, rendered)
        if relative == "index.html":
            title = {"fr": "AdeeboMeter — dépannage réseau gratuit pour Windows", "es": "AdeeboMeter — monitor de red gratuito para Windows", "bn": "AdeeboMeter — Windows-এ ধীর ইন্টারনেট ও পিসি পরীক্ষা", "ar": "AdeeboMeter — أداة مجانية لفحص الشبكة على Windows", "zh": "AdeeboMeter — 免费 Windows 网络与电脑诊断工具"}[LANGUAGE]
            rendered = re.sub(r"<title>.*?</title>", "<title>" + title + "</title>", rendered)
            rendered = re.sub(r'(<meta property="og:title" content=")[^"]*', lambda m: m[1] + title, rendered)
        localized_url = url.replace(SITE, SITE + LANGUAGE + "/")
        meta_description = re.search(r'<meta name="description" content="([^"]*)"', rendered)
        def localized_schema(match):
            data = json.loads(match[1])
            if data.get("@type") == "SoftwareApplication" or (LANGUAGE in ("es", "zh") and data.get("@type") in ("WebSite", "LearningResource")):
                data["url"] = localized_url
                data["inLanguage"] = LANG_TAG
                if meta_description:
                    data["description"] = html.unescape(meta_description[1])
                if LANGUAGE in ("es", "zh") and data.get("@type") == "LearningResource":
                    data["name"] = translate(data["name"])
                    data["about"] = translate(data.get("about", ""))
                    if isinstance(data.get("isPartOf"), dict):
                        data["isPartOf"]["url"] = SITE + LANGUAGE + "/"
            return '<script type="application/ld+json">' + json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "</script>"
        rendered = re.sub(r'<script type="application/ld\+json">(.*?)</script>',
                          localized_schema, rendered, flags=re.S)
        if 'property="og:locale"' not in rendered:
            rendered = rendered.replace("</head>", '<meta property="og:locale" content="' + {"fr": "fr_CA", "es": "es_ES", "bn": "bn_BD", "ar": "ar_AR", "zh": "zh_CN"}[LANGUAGE] + '">\n</head>')
        if relative == "index.html":
            rendered = re.sub(r'(<ul class="v4trust">.*?)(</ul>)',
                              r"\1<li>" + {"fr": "Sans publicité", "es": "Sin anuncios", "bn": "বিজ্ঞাপন নেই", "ar": "بدون إعلانات", "zh": "无广告"}[LANGUAGE] + r"</li>\2", rendered, flags=re.S)
        if 'data-language-picker' not in rendered:
            english_link = f'<a href="{url}?lang=en" lang="en">English</a>'
            if "<nav" in rendered:
                rendered = rendered.replace("</nav>", " · " + english_link + "</nav>", 1)
            else:
                rendered = re.sub(r"(<main[^>]*>)", lambda m: m[1] + '<nav class="language-links" aria-label="' + {"fr": "Langue", "es": "Idioma", "ar": "اللغة", "bn": "ভাষা", "zh": "语言"}[LANGUAGE] + '">' + english_link + "</nav>", rendered, count=1)
    target = DOCS / LANGUAGE / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    lt = json.loads((ROOT / "tools/learn-tags.json").read_text(encoding="utf-8")).get(LANGUAGE, {}).get(Path(relative).stem) if relative.startswith("learn/") else None
    if lt:   # R2-202 / R2-203: the newest lessons' own one-line summary, translated
        rendered = re.sub(r'(<meta (?:name="description"|property="og:description"|name="twitter:description") content=")[^"]*', lambda m: m[1] + html.escape(lt, quote=True), rendered)
    stage(target, rendered.rstrip() + "\n")
    english = staged(source)
    english = re.sub(r'<link rel="alternate" hreflang="(?:en|bn|fr(?:-CA)?|es|ar|zh(?:-Hans)?|x-default)"[^>]*>\s*', "", english)
    variants = route_languages(url)
    def route_attributes(match):
        attrs = re.sub(r' data-language-(?:route|variants)="[^"]*"', "", match[1])
        return '<html' + attrs + ' data-language-route="' + urlsplit(url).path + '" data-language-variants="' + variants + '">'
    english = re.sub(r"<html([^>]*)>", route_attributes, english, count=1)
    english = english.replace("</head>", language_links(url.replace(SITE, SITE + LANGUAGE + "/")) + "</head>")
    stage(source, english)
    for other_language in ("bn", "fr", "es", "ar", "zh"):
        bengali = DOCS / other_language / relative
        if other_language == LANGUAGE or not bengali.exists():
            continue
        existing = staged(bengali)
        existing = re.sub(r'<link rel="alternate" hreflang="(?:en|bn|fr(?:-CA)?|es|ar|zh(?:-Hans)?|x-default)"[^>]*>\s*', "", existing)
        existing = re.sub(r"<html([^>]*)>", route_attributes, existing, count=1)
        existing = existing.replace("</head>", language_links(url) + "</head>")
        stage(bengali, existing)

# R2-185: nothing is written until every page rendered - a failure part-way leaves the site exactly as it was
for path, text in pending.items():
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_text(encoding="utf-8") != text:
        path.write_text(text, encoding="utf-8", newline="\n")

sitemap = DOCS / "sitemap.xml"
xml = sitemap.read_text(encoding="utf-8")
if LANGUAGE in ("fr", "es", "ar"):
    xml = re.sub(rf"^.*<loc>https://adeebometer.com/\?lang={LANGUAGE}</loc>.*\n?", "", xml, flags=re.M)
for source in files:
    relative = source.relative_to(DOCS).as_posix()
    route = LANGUAGE + "/" + (relative[:-10] if relative.endswith("index.html") else relative)
    url = SITE + route
    if "<loc>" + url + "</loc>" not in xml:
        xml = xml.replace("</urlset>", "  <url><loc>" + url + "</loc></url>\n</urlset>")
sitemap.write_text(xml, encoding="utf-8", newline="\n")
print(f"Generated {len(files)} {LANG_TAG} pages.")

# The DNS lesson's video box is written per language by published-tutorials.py; rebuilding the lesson above from the
# English page put it back in English (21 Nov) - so add the translated box again, now that this language's pages exist.
runpy_tutorials = __import__("runpy").run_path(str(Path(__file__).with_name("published-tutorials.py")))
runpy_tutorials["install"]()
