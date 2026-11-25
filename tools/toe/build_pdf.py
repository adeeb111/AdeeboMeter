"""Build docs/AdeeboMeter-Theory-of-Everything.pdf from the Theory of Everything slides on docs/index.html.
The website's slides stay as they are; this adds the release's own slides and version, numbers them, and prints the deck
at 1280 x 720 with Chromium (print styles from index.html).   python3 tools/toe/build_pdf.py"""
import re, asyncio
from pathlib import Path
from playwright.async_api import async_playwright

ROOT = Path(__file__).resolve().parents[2]; DOCS = ROOT / "docs"
VERSION = "1.1.6"
html = (DOCS / "index.html").read_text(encoding="utf-8")
html = re.sub(r"The Theory of Everything · version [0-9.]+", "The Theory of Everything · version " + VERSION, html)
html = html.replace("100+ tools. Type what you need.", "232 tools. Type what you need.")
html = html.replace("<h3>No shell, ever</h3><p>Windows tools by full System32 path, fixed arguments, a timeout, inside a job object that dies with the app.</p>",
                    "<h3>No system shell, ever</h3><p>Windows tools by full System32 path, fixed arguments, a timeout, inside a job object that dies with the app. AdeeboShell is its own - it starts nothing.</p>")

def slide(kicker, title, body):
    return ('<div class="frame"><section class="slide"><div class="in">\n'
            f'  <div class="kicker">{kicker}</div>\n  <h2>{title}</h2>\n  <div class="body">\n{body}\n  </div>\n'
            '  <div class="foot"><span><b>AdeeboMeter</b> · Theory of Everything</span><span>00</span></div>\n</div></section></div>\n')

def cards(cls, items):
    return f'    <div class="grid {cls}">\n' + "\n".join(
        f'      <div class="card{(" " + c) if c else ""}">{("<span class=num>" + n + "</span>") if n else ""}<h3>{h}</h3><p>{p}</p></div>' for c, n, h, p in items) + "\n    </div>"

NEW = [
    slide("New in 1.1.6 · languages", "Five languages, one switch.",
          cards("g3", [("b", "", "English, Français, Español, العربية, বাংলা", "The globe beside VIEW opens a card per language with a preview. Nothing changes until you click one; a restart finishes the last screens."),
                       ("", "", "Right to left", "Arabic turns the whole window around, so it reads the way Arabic is written."),
                       ("", "", "Plain tables", "Each language is a text file: the English, a tab, the translation. A checker counts what's missing - all four are at 100%."),
                       ("", "", "The buddy too", "Every persona speaks each language; the website comes in all five."),
                       ("g", "", "English on purpose", "Commands, program names and the shell's hints stay English - they are what you type."),
                       ("", "", "Never a mix-up", "A missing line shows the English, never another language's text.")])),
    slide("New in 1.1.6 · AdeeboShell", "The whole app from the keyboard.",
          cards("g4", [("b", "", "Every tool is a command", "<code>ping 1.1.1.1 20</code>, <code>dig example.com MX</code>, <code>mtr 1.1.1.1 1</code> - the same checks and answers as the Toolbox."),
                       ("", "", "Pages are folders", "<code>cd pages/markets</code> then <code>open</code>. <code>/tools</code> holds every tool by group; <code>/proc</code> is this PC as files."),
                       ("", "", "A real shell", "Pipes, redirects, variables, <code>$( )</code>, <code>$(( ))</code>, if / for / while / case, functions, arrays, <code>parallel</code>, jobs, nano and vim."),
                       ("", "", "Shows what it's doing", "A passing line names the quiet command; <code>alltools</code> counts [n/N] and streams each tool's output."),
                       ("", "", "Find your way", "After every <code>cd</code> a hint says where you are and what to type next; <code>home</code> goes back; <code>help</code>, <code>man</code>, Tab."),
                       ("", "", "Read it back", "A scroll bar, 8,000 lines, the view holds still while output arrives; Copy all and Save log in the window bar."),
                       ("", "", "Ready-made scripts", "network, system, destinations, Wi-Fi, before a call, ISP evidence, security, health - in /data/scripts, yours to change."),
                       ("g", "", "Ctrl+`", "From anywhere in the app.")])),
    slide("New in 1.1.6 · AdeeboShell's fence", "It looks like a shell. It can't hurt your PC.",
          cards("g3", [("g", "1", "Never starts a program", "No cmd, no PowerShell, no .exe. The one exception: <code>open /data</code> shows AdeeboMeter's own folder in File Explorer."),
                       ("g", "2", "Changes only /data", "Every write - &gt;, tee, touch, mkdir, rm, cp, mv, sed -i, the editor - asks first. Your Windows folder and drives are read-only."),
                       ("g", "3", "No side doors", "A name ending in a dot or space, a junction, a symlink or a hard link can't carry a write outside /data."),
                       ("", "", "Consent can't be scripted", "Test traffic needs <code>--yes</code> typed at the prompt. A script, a pipe or a scheduled run never counts."),
                       ("", "", "Limits everywhere", "Output, nesting, recursion and run time are capped, so a script can't freeze the app."),
                       ("", "", "Copy in, don't reach out", "<code>cp ~/Downloads/x.sh /data/scripts/</code> - read anything, change only your own folder.")])),
    slide("New in 1.1.6 · AdeeboLab", "Draw it. Break it. Watch where it fails.",
          cards("g3", [("b", "", "From what it measured", "See my LAN, My path, Wi-Fi around you, phone vs PC, VPN split - drawn for you."),
                       ("", "", "Or draw your own", "Pick a device, click to place, link two with cable, Wi-Fi, 4G/5G or fibre; text and areas label it."),
                       ("", "", "Break it", "Pick one fault - DNS, MTU, loss, a wrong gateway - press play and watch the packet stop."),
                       ("", "", "Edit like a drawing app", "Drag (it follows the pointer, snaps on release), double-click or F2 to rename, the blue corner to resize."),
                       ("", "", "Examples", "Gamer, On 4G / 5G, Trader, Streamer, IPTV viewer, Server owner - ready to break."),
                       ("g", "", "Data, never code", "A drawing someone sends is read field by field and size-capped - nothing in a file can run.")])),
    slide("New in 1.1.6 · keyboard and patience", "No mouse needed. No silent waits.",
          cards("g4", [("b", "", "Tab and Enter", "Every tile, button and link on a page, in reading order."),
                       ("b", "", "Arrow keys", "Between tiles - and across the window: sidebar ⇄ page ⇄ header and tab rows."),
                       ("", "", "Shortcuts", "Ctrl+K search, Ctrl+1…7 sections, Ctrl+` shell, F1 explains, Esc back to the corner."),
                       ("", "", "Ctrl+Alt+S", "From anywhere in Windows: \"it feels slow right now\" - recorded, and whose fault it was."),
                       ("g", "", "Waiting? It says so", "Anything over 5 seconds - a tool, Windows' Wi-Fi report, a WMI query - shows in the footer with its seconds."),
                       ("", "", "Then it's gone", "The note clears the moment the work ends."),
                       ("", "", "Calm at logon", "The first 45 s while Windows joins the network show \"waiting\", not \"Not connected\"."),
                       ("", "", "Judged on enough", "Grades, the provider letter and the six-month review wait for a day of history.")])),
    slide("New in 1.1.6 · the corner, Markets and your ISP", "Calmer corner. More to watch. Proof on one page.",
          cards("g4", [("b", "", "Rest, then it brightens", "The widget wakes after a short rest (0.8 s, Settings › Widget › Hover wait) and shows one quiet hint."),
                       ("b", "", "Never in the way", "Clicks, right-clicks, the wheel and taps go to the app underneath. The keyboard is never watched."),
                       ("", "", "Move it on purpose", "Rest 2.5 s to unlock it, or tray › Move widget. Then drag, or tap to open AdeeboMeter."),
                       ("", "", "Night sky", "The weather shows a moon after sunset."),
                       ("g", "", "Companies", "Pick from the big names or search; follow them and put up to four on the widget."),
                       ("g", "", "Three price services", "Your own free key from Financial Modeling Prep, Finnhub or Twelve Data - if one can't answer, the next does."),
                       ("", "", "A new ISP report", "The answer and the five stops on page one, then the message to paste, hop by hop and when it happens."),
                       ("", "", "One pop-up style", "Every question box in the same calm card, dark title bar and one blue OK.")])),
]
marker = '<div class="frame"><section class="slide"><div class="in">\n  <div class="kicker">Get it</div>'
assert marker in html, "the Get it slide moved"
html = html.replace(marker, "".join(NEW) + marker, 1)

# number every slide's footer in order
n = [0]
def num(m):
    n[0] += 1; return m.group(1) + f"{n[0]:02d}" + m.group(2)
html = re.sub(r'(<div class="foot"><span>.*?</span><span>)\d+(</span></div>)', num, html)
tmp = DOCS / "_toe_print.html"; tmp.write_text(html, encoding="utf-8")

async def main():
    async with async_playwright() as p:
        b = await p.chromium.launch(); pg = await b.new_page(viewport={"width": 1280, "height": 720})
        await pg.goto(tmp.as_uri(), wait_until="load"); await pg.wait_for_timeout(800)
        await pg.add_style_tag(content='body,.slide{font-family:"Inter",system-ui,sans-serif!important}')
        await pg.evaluate("document.querySelectorAll('details').forEach(function(d){d.open=true})")   # the slides sit in the Theory fold on the site (closed by default)
        await pg.emulate_media(media="print")
        await pg.pdf(path=str(DOCS / "AdeeboMeter-Theory-of-Everything.pdf"), width="1280px", height="720px", print_background=True)
        await b.close()
try: asyncio.run(main())
finally: tmp.unlink(missing_ok=True)
print("slides:", n[0])
