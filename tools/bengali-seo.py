"""Generate crawlable Bengali Learn pages from the reviewed website dictionary.

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
messages = json.loads(raw[raw.index("{"):raw.rindex("}") + 1])["bn"]
messages.update({
    "To capture ping and traceroute results together,": "পিং ও ট্রেসরাউটের ফল একসঙ্গে সংগ্রহ করতে",
    "create a packet loss report for your internet provider": "ইন্টারনেট সেবাদাতার জন্য প্যাকেট লসের রিপোর্ট তৈরি করুন",
    "Compare memory use with CPU and disk activity using": "মেমরির ব্যবহার, CPU ও ডিস্কের কাজকর্ম একসঙ্গে দেখুন",
    "AdeeboMeter’s Windows PC monitor": "আদীবওমিটারের Windows পিসি পর্যবেক্ষণে",
    "To see how both change while you work,": "কাজের সময় দুটির পরিবর্তন দেখতে",
    "monitor internet speed on Windows": "Windows-এ ইন্টারনেটের গতি পর্যবেক্ষণ করুন",
    "If the connection keeps dropping, compare Wi-Fi with Ethernet and": "সংযোগ বারবার বিচ্ছিন্ন হলে Wi-Fi ও Ethernet তুলনা করুন এবং",
    "save a connection report for your internet provider": "ইন্টারনেট সেবাদাতার জন্য সংযোগের রিপোর্ট রাখুন",
})
files = sorted((DOCS / "learn").glob("*.html"))
localized = {"/learn/" + p.name for p in files}
localized.update({"/learn/", "/", "/blog/", "/wifi-or-isp.html",
                  "/internet-speed-monitor.html", "/pc-monitoring.html", "/isp-report.html"})

def translate(text):
    key = text.strip()
    translated = messages.get(key)
    if translated is None:
        match = re.fullmatch(r"(← )?(.+?)( →| \| Learn \| AdeeboMeter)?", key)
        if match:
            prefix, title, suffix = match.groups()
            translated_title = messages.get(title, title)
            if suffix == " | Learn | AdeeboMeter":
                suffix = " | " + messages.get("Learn", "শিখুন") + " | আদীবওমিটার"
            translated = (prefix or "") + translated_title + (suffix or "")
    return text.replace(key, translated or key, 1) if key else text

def language_links(url):
    english = url.replace("/bn/", "/", 1)
    return (f'<link rel="alternate" hreflang="en" href="{english}">\n'
            f'<link rel="alternate" hreflang="bn" href="{url}">\n')

class BengaliPage(HTMLParser):
    def __init__(self, source_url):
        super().__init__(convert_charrefs=False)
        self.source_url = source_url
        self.output = []
        self.excluded = []
        self.in_title = False

    def handle_starttag(self, tag, attrs):
        data = dict(attrs)
        if tag in ("script", "style", "svg", "code", "pre"):
            self.excluded.append(tag)
        self.in_title = self.in_title or tag == "title"
        if tag == "html":
            data["lang"] = "bn"
            data["data-language-route"] = urlsplit(self.source_url).path
        for attr in ("href", "src", "action"):
            value = data.get(attr, "")
            if not value or value.startswith(("#", "data:", "mailto:", "javascript:")):
                continue
            target = urlsplit(urljoin(self.source_url, value))
            if target.netloc != "adeebometer.com":
                continue
            route = target.path
            if attr == "href" and route in localized:
                route = "/bn" + route
            data[attr] = route + ("?" + target.query if target.query else "") + ("#" + target.fragment if target.fragment else "")
        if tag == "link" and data.get("rel") == "canonical":
            data["href"] = self.source_url.replace(SITE, SITE + "bn/")
        for attr in ("aria-label", "placeholder", "alt", "title"):
            if attr in data:
                data[attr] = translate(html.unescape(data[attr]))
        if tag == "meta" and data.get("property") == "og:title":
            data["content"] = translate(data["content"])
        rendered = "".join(" " + key + ('="' + html.escape(value, quote=True) + '"' if value is not None else "")
                           for key, value in data.items())
        self.output.append("<" + tag + rendered + ">")

    def handle_endtag(self, tag):
        if tag == "head":
            url = self.source_url.replace(SITE, SITE + "bn/")
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

for source in files:
    relative = "learn/" + source.name
    url = SITE + ("learn/" if source.name == "index.html" else relative)
    parser = BengaliPage(url)
    # Decode character references before text translation without altering markup.
    content = source.read_text(encoding="utf-8-sig")
    content = re.sub(r'<link rel="alternate" hreflang="(?:en|bn)"[^>]*>\s*', "", content)
    content = re.sub(r">([^<]+)<", lambda m: ">" + html.escape(html.unescape(m[1]), quote=False) + "<", content)
    # Parser assembles split entities into text by using normal conversion.
    parser.convert_charrefs = True
    parser.feed(content)
    rendered = "".join(parser.output)
    title = re.search(r"<title>(.*?)</title>", rendered, re.S)[1]
    paragraphs = re.findall(r"<p[^>]*>(.*?)</p>", rendered, re.S)
    paragraph = next((re.sub("<[^>]+>", "", p) for p in paragraphs if re.search("[অ-হ]", p)), "")
    description = html.escape(html.unescape(paragraph or title).strip()[:170], quote=True)
    rendered = re.sub(r'(<meta (?:name="description"|property="og:description") content=")[^"]*',
                      lambda m: m[1] + description, rendered)
    target = DOCS / "bn" / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(rendered, encoding="utf-8", newline="\n")
    english = source.read_text(encoding="utf-8-sig")
    english = re.sub(r'<link rel="alternate" hreflang="(?:en|bn)"[^>]*>\s*', "", english)
    english = english.replace("</head>", language_links(url.replace(SITE, SITE + "bn/")) + "</head>")
    source.write_text(english, encoding="utf-8", newline="\n")

sitemap = DOCS / "sitemap.xml"
xml = sitemap.read_text(encoding="utf-8")
for source in files:
    route = "bn/learn/" + ("" if source.name == "index.html" else source.name)
    url = SITE + route
    if "<loc>" + url + "</loc>" not in xml:
        xml = xml.replace("</urlset>", "  <url><loc>" + url + "</loc></url>\n</urlset>")
sitemap.write_text(xml, encoding="utf-8", newline="\n")
print(f"Generated {len(files)} Bengali Learn pages.")
