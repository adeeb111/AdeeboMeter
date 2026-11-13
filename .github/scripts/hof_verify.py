#!/usr/bin/env python3
"""Hall of Fame check for AdeeboMeter award cards.

Reads one entry (from a GitHub issue made with the "Hall of Fame" form, or from the inputs of a manual run for an
entry that came by email), computes the claim code with the secret key - exactly as AdeeboMeter's src/HallOfFame.cs
does - and only on a match adds the entry to docs/hall-of-fame.json.

Never trusts the text it reads: the issue body arrives in an environment variable (never pasted into a shell line),
names are cleaned to the same safe characters the app allows, a social handle must fit that site's own rules (the page
builds the link itself - never one typed in), and the page shows every field as text (never as HTML).

Exit codes: 0 added (or already there), 2 the code doesn't match, 3 something is missing or wrong in the entry.
Writes a short result to $GITHUB_OUTPUT (result=..., message=...) for the workflow to comment on the issue.
"""
import base64, datetime, hashlib, hmac, json, os, re, sys, unicodedata

JSON = os.path.join(os.path.dirname(__file__), "..", "..", "docs", "hall-of-fame.json")
AWARD_AT = 500
MAX_POINTS = 900   # AdeeboMeter never counts more (Points.Max): everything there is to earn is about 810


def clean_name(n):
    # the same rule as HallOfFame.CleanName: NFC; letters and their marks (L*, Mn, Mc, any plane), decimal digits,
    # . - _ ' ; single spaces; at most 32 characters (code points)
    out, sp, marks = [], False, 0
    for c in unicodedata.normalize("NFC", (n or "").strip()):
        if c.isspace():
            if not sp and out:
                out.append(" ")
            sp = True
            continue
        cat = unicodedata.category(c)
        if cat[0] == "L" or cat in ("Mn", "Mc", "Nd") or c in ".-_'":
            mark = cat in ("Mn", "Mc")
            if mark and (not out or out[-1] == " " or marks >= 2):
                continue  # a mark with no letter under it, or a third one on the same letter
            out.append(c)
            sp = False
            marks = marks + 1 if mark else 0
    while out and out[-1] == " ":
        out.pop()
    if len(out) > 32:
        out = out[:32]
        while out and out[-1] == " ":
            out.pop()
    return "".join(out)


def script(c):
    o = ord(c)
    if "A" <= c <= "Z" or "a" <= c <= "z" or (0xC0 <= o <= 0x24F and o not in (0xD7, 0xF7)) or 0x1E00 <= o <= 0x1EFF:
        return 1
    if 0x400 <= o <= 0x52F:
        return 2
    if 0x370 <= o <= 0x3FF or 0x1F00 <= o <= 0x1FFF:
        return 3
    return 0


def mixed_script(name):
    # the same rule as HallOfFame.MixedScript: one word mixing Latin with Cyrillic or Greek is a look-alike of someone else
    for w in name.split(" "):
        seen = 0
        for c in w:
            s = script(c)
            if s and seen and s != seen:
                return True
            if s:
                seen = s
    return False


def same_person(a, b):
    # one entry per name: case, accents' forms and look-alike widths don't make a new person
    f = lambda x: unicodedata.normalize("NFKC", x).casefold().replace(" ", "")
    return f(a) == f(b)


def code(key, name, total, date):
    mac = hmac.new(key.encode("utf-8"), ("v1|%s|%d|%s" % (clean_name(name), total, date)).encode("utf-8"), hashlib.sha256).digest()
    b = base64.b32encode(mac).decode("ascii")[:16]
    return "ADB-%s-%s-%s-%s" % (b[0:4], b[4:8], b[8:12], b[12:16])


def field(body, label):
    # an issue form body: "### Label\n\nvalue\n\n### Next"
    m = re.search(r"^###\s+" + re.escape(label) + r"\s*\n+(.*?)(?=\n###\s|\Z)", body or "", re.S | re.M)
    v = (m.group(1).strip() if m else "")
    return "" if v == "_No response_" else v


# where a winner can be found: the site, the characters its handles allow, and the profile link the page builds from it
SOCIAL = {
    "github": (r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?", "github.com/"),
    "linkedin": (r"[A-Za-z0-9-]{3,100}", "linkedin.com/in/"),
    "x": (r"[A-Za-z0-9_]{1,15}", "x.com/"),
    "instagram": (r"[A-Za-z0-9_.]{1,30}", "instagram.com/"),
    "bluesky": (r"[A-Za-z0-9.-]{3,253}", "bsky.app/profile/"),
}


def social(where, handle):
    # ("x", "ada") from the form's two fields; ("", "") when there's none or it doesn't fit the site's rules
    w = (where or "").strip().lower()
    w = {"github": "github", "linkedin": "linkedin", "x": "x", "instagram": "instagram", "bluesky": "bluesky"}.get(w, "")
    h = (handle or "").strip()
    if not w or not h:
        return "", ""
    rx, base = SOCIAL[w]
    m = re.search(r"(?:https?://)?(?:www\.)?" + re.escape(base) + r"([^/?#\s]+)", h, re.I)   # a pasted profile link
    if m:
        h = m.group(1)
    h = h.lstrip("@").strip().rstrip("/")
    return (w, h) if re.fullmatch(rx, h) else ("", "")


def out(result, message):
    p = os.environ.get("GITHUB_OUTPUT")
    if p:
        with open(p, "a", encoding="utf-8") as f:
            f.write("result=%s\n" % result)
            f.write("message<<EOM\n%s\nEOM\n" % message)
    print(result + ": " + message)


def save(data):
    with open(JSON, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")


def main():
    key = os.environ.get("HOF_KEY", "")
    if not key:
        out("error", "The Hall of Fame key isn't set up yet (repository secret HOF_KEY).")
        return 3
    body = os.environ.get("ISSUE_BODY", "")
    if body:
        name, total, date, claim = field(body, "Name on the card"), field(body, "Points on the card"), field(body, "Date on the card"), field(body, "Claim code")
        where, handle = field(body, "Where people can find you (optional)"), field(body, "Your handle (optional)")
        via, ref = "form", os.environ.get("ISSUE_URL", "")
    else:
        name, total, date, claim, where, handle = (os.environ.get(k, "") for k in ("IN_NAME", "IN_POINTS", "IN_DATE", "IN_CODE", "IN_WHERE", "IN_HANDLE"))
        via, ref = "email", ""
    name = clean_name(name)
    # a code pasted from Word or Outlook: non-breaking hyphens, en dashes, full-width letters - folded back to plain ASCII
    claim = unicodedata.normalize("NFKC", claim or "")
    claim = re.sub(r"[\u2010-\u2015\u2212\uFE58\uFE63\uFF0D]", "-", claim)
    claim = re.sub(r"\s+", "", claim).upper()
    if claim and not re.fullmatch(r"[A-Z0-9-]{1,40}", claim):
        out("mismatch", "The claim code has characters that aren't on the card - type it as it is shown (letters, numbers and dashes).")
        return 2
    if not name or not re.fullmatch(r"[1-9]\d{0,5}", (total or "").strip()) or not re.fullmatch(r"\d{4}-\d{2}-\d{2}", (date or "").strip()):
        out("missing", "The name, points and date have to be exactly as they are on the card (points like 512, date like 2026-11-12).")
        return 3
    if mixed_script(name):
        out("mismatch", "A word in the name mixes Latin letters with Cyrillic or Greek look-alikes - AdeeboMeter never makes a card like that.")
        return 2
    total, date = int(total.strip()), date.strip()
    try:
        d = datetime.date.fromisoformat(date)
    except ValueError:
        out("missing", "The date on the card isn't a real date.")
        return 3
    if total < AWARD_AT or total > MAX_POINTS or d < datetime.date(2026, 11, 1) or d > datetime.date.today() + datetime.timedelta(days=1):
        out("mismatch", "That card isn't an Adeebo Award card (500 to %d points, made with AdeeboMeter 1.1.5 or later)." % MAX_POINTS)
        return 2
    if not hmac.compare_digest(code(key, name, total, date), claim):
        out("mismatch", "The claim code doesn't match the name, points and date. Type them exactly as they are on the card - or make the card again in AdeeboMeter.")
        return 2
    site, handle = social(where, handle)
    author = os.environ.get("ISSUE_AUTHOR", "")
    if not re.fullmatch(r"[A-Za-z0-9-]{1,39}", author or ""):
        author = ""
    # a GitHub link must be the account that sent it: a card is made to be shared, so anyone can type its code
    if site == "github" and handle.lower() != author.lower():
        site, handle = "", ""
    with open(JSON, encoding="utf-8") as f:
        data = json.load(f)
    entries = data.setdefault("entries", [])
    mine = next((e for e in entries if e.get("code") == claim), None)
    if mine is not None:
        by = (mine.get("by") or "").lower()
        if author and by == author.lower():   # the same sender again: let them fix their link
            mine["site"], mine["handle"] = site, handle
            save(data)
            out("updated", "Updated your link in the Hall of Fame, %s." % name)
        elif author and by and by != author.lower():   # a second sender for the same card: nobody's link until Adeeb checks
            mine["site"], mine["handle"], mine["disputed"] = "", "", True
            save(data)
            out("review", "Someone else already sent this card. Its link is off until Adeeb checks who it belongs to - reply on this issue with anything that shows it's yours.")
        else:
            out("already", "%s is already in the Hall of Fame." % name)
        return 0
    if any(same_person(e.get("name", ""), name) for e in entries):
        out("already", "There's already a %s in the Hall of Fame. If that isn't you, put something extra in your name on the card (an initial or your city) and send the new card." % name)
        return 0
    entries.append({"name": name, "points": total, "date": date, "code": claim, "site": site, "handle": handle, "via": via, "by": author, "ref": ref if re.fullmatch(r"https://github\.com/adeeb111/AdeeboMeter/issues/\d+", ref or "") else ""})
    entries.sort(key=lambda e: (e["date"], e["name"].lower()))
    save(data)
    out("added", "Welcome to the Hall of Fame, %s! You'll be on the page in a minute or two: https://adeeb111.github.io/AdeeboMeter/hall-of-fame.html" % name)
    return 0


if __name__ == "__main__":
    if len(sys.argv) == 5 and sys.argv[1] == "--code":   # for testing: hof_verify.py --code NAME POINTS DATE (key from HOF_KEY)
        print(code(os.environ.get("HOF_KEY", ""), sys.argv[2], int(sys.argv[3]), sys.argv[4]))
        sys.exit(0)
    sys.exit(main())
