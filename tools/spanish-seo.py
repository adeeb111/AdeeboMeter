"""Build Spanish SEO pages from the approved website translations."""
import runpy
from pathlib import Path

runpy.run_path(str(Path(__file__).with_name("bengali-seo.py")), init_globals={"LANGUAGE": "es"})
