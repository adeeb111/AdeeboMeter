"""Build guide FAQ metadata from the visible localized questions and answers.

Run after language generation. This describes content, not Google rich-result eligibility.
"""
import json
import re
from html.parser import HTMLParser
from pathlib import Path

DOCS = Path(__file__).resolve().parents[1] / "docs"


class Questions(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.active = False
        self.summary = False
        self.question = []
        self.answer = []
        self.items = []
        self.lang = "en"
        self.url = None

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "html":
            self.lang = attrs.get("lang", "en")
        if tag == "link" and attrs.get("rel") == "canonical":
            self.url = attrs["href"]
        if tag == "details" and "data-guide-faq" in attrs:
            self.active = True
            self.question, self.answer = [], []
        if self.active and tag == "summary":
            self.summary = True

    def handle_endtag(self, tag):
        if tag == "summary":
            self.summary = False
        if self.active and tag == "p":
            self.answer.append(" ")
        if self.active and tag == "details":
            question = " ".join("".join(self.question).split())
            answer = " ".join("".join(self.answer).split())
            if not question or not answer:
                raise ValueError("FAQ needs both a visible question and answer")
            self.items.append({"@type": "Question", "name": question,
                               "acceptedAnswer": {"@type": "Answer", "text": answer}})
            self.active = False

    def handle_data(self, data):
        if self.active:
            (self.question if self.summary else self.answer).append(data)


def install():
    count = 0
    for lang in ("", "bn", "fr", "es", "ar"):
        for name in ("adeeboshell.html", "adeebolab-guide.html"):
            path = DOCS / lang / name
            if not path.exists():
                continue
            source = path.read_text(encoding="utf-8")
            source = re.sub(r'<script[^>]*id="guide-faq-schema"[^>]*>.*?</script>\s*', "", source, flags=re.S)
            parser = Questions()
            parser.feed(source)
            if parser.items:
                if not parser.url:
                    raise ValueError(f"Missing canonical: {path}")
                data = {"@context": "https://schema.org", "@type": "FAQPage",
                        "@id": parser.url + "#faq", "url": parser.url,
                        "inLanguage": parser.lang, "mainEntity": parser.items}
                payload = json.dumps(data, ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c")
                source = source.replace("</head>", '<script type="application/ld+json" id="guide-faq-schema">' + payload + '</script>\n</head>', 1)
            if source != path.read_text(encoding="utf-8"):
                path.write_text(source, encoding="utf-8", newline="\n")
            count += len(parser.items)
    print(f"Guide FAQ metadata: {count} visible answers")


if __name__ == "__main__":
    install()
