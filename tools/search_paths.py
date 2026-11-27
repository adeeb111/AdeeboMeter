"""Add focused English lesson-to-product paths after Learn generation, before localization."""
import html
import re
from pathlib import Path
DOCS = Path(__file__).resolve().parents[1] / "docs"
START = "<!--english-search-paths:start-->"
END = "<!--english-search-paths:end-->"
PATHS = {
 "learn/dns.html": ("Try the DNS explanation", [("/adeebolab-guide.html", "Simulate a DNS timeout in AdeeboLab"), ("/wifi-or-isp.html#connected-no-internet", "Wi-Fi connected but nothing loads? Check router, internet and DNS")]),
 "learn/icmp.html": ("Use the probe carefully", [("/adeeboshell.html", "Explore ping help in AdeeboShell"), ("/blog/mtr-one-hop-packet-loss.html", "One MTR hop shows loss: read the destination too")]),
 "learn/wifi.html": ("Compare the same connection", [("/blog/wifi-disconnects-ethernet-works.html", "Wi-Fi disconnects but Ethernet works"), ("/adeebolab-guide.html", "Follow a simulated packet through AdeeboLab")]),
 "learn/mtu-path.html": ("From the path to a useful report", [("/adeeboshell.html", "Find network tools in AdeeboShell"), ("/isp-report.html", "Keep a comparison for your provider")]),
 "learn/stream-fault.html": ("Check the playback problem", [("/blog/fast-speed-test-buffering.html", "Fast speed test, but videos still buffer"), ("/adeebolab-guide.html", "Change one simulated fault in AdeeboLab")]),
 "learn/speed-vs-delay.html": ("Separate the two symptoms", [("/blog/game-lag-or-low-fps.html", "Game lag or low FPS? Separate network and rendering"), ("/adeeboshell.html", "Read connection status in AdeeboShell")]),
 "learn/open-website.html": ("Find the step that stopped", [("/adeebolab-guide.html", "See a DNS timeout versus a wrong gateway in AdeeboLab"), ("/wifi-or-isp.html#connected-no-internet", "Run three checks when Wi-Fi connects but nothing loads")]),
 "learn/apps-internet.html": ("Try a real comparison", [("/adeeboshell.html", "Explore the built-in Windows network checks"), ("/pc-monitoring.html", "Find the busy app when your PC slows down")]),
 "learn/index.html": ("Try what you learned", [("/adeebolab-guide.html", "AdeeboLab: learn with a simulated network fault"), ("/adeeboshell.html", "AdeeboShell: run a check on your Windows PC")]),
 "blog/index.html": ("Two ways to investigate", [("/adeebolab-guide.html", "AdeeboLab: see why a packet stops"), ("/adeeboshell.html", "AdeeboShell: run the real network check")]),
}
def install():
 for route, (heading, links) in PATHS.items():
  p = DOCS / route
  s = re.sub(re.escape(START) + r".*?" + re.escape(END) + r"\s*", "", p.read_text(encoding="utf-8"), flags=re.S)
  rows = "".join('<p><a href="' + url + '">' + html.escape(label) + '</a></p>' for url, label in links)
  css = "card" if route.startswith("learn/") else "example"
  block = START + '<nav class="' + css + '" aria-labelledby="practice-heading"><h2 id="practice-heading">' + heading + '</h2>' + rows + '</nav>' + END + "\n"
  assert s.count('</main>') == 1, route
  s = s.replace('</main>', block + '</main>', 1)
  p.write_text(s, encoding="utf-8", newline="\n")
 print("Search paths: " + str(len(PATHS)) + " English pages")
if __name__ == "__main__":
 install()
