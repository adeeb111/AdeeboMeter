"""Build Simplified Chinese routes from the canonical website dictionary."""
import runpy
import sys
from pathlib import Path
options = {"LANGUAGE": "zh"}
if "--home-only" in sys.argv:
    options["PAGE_FILTER"] = {"index.html"}
runpy.run_path(str(Path(__file__).with_name("bengali-seo.py")), init_globals=options)
