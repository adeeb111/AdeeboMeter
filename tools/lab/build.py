"""Build docs/lab.html (the Network Lab) from lab.src.html + glyphs.json (icon shapes, TopoDraw's set).
python3 tools/lab/build.py   - then python3 tools/learnsite.py to refresh sw.js. The app ships the same docs/lab.html."""
from pathlib import Path
here = Path(__file__).resolve().parent
src = (here / "lab.src.html").read_text(encoding="utf-8")
glyphs = (here / "glyphs.json").read_text(encoding="utf-8").strip()
out = here.parents[1] / "docs" / "lab.html"
src = src.replace("/*__PROJECT_TRANSFER__*/", (here / "project-transfer.js").read_text(encoding="utf-8"))
out.write_text(src.replace("__GLYPHS__", glyphs).replace("/*__QR__*/", (here.parents[1] / "docs/qr.js").read_text(encoding="utf-8")), encoding="utf-8", newline="\n")
print(f"{out.name}: {out.stat().st_size:,} bytes")
