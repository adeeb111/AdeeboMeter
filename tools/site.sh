#!/bin/sh
# Rebuild the website after changing pages, in the order that matters (21 Nov): the lab page, the Learn pages and the
# offline cache first, then the language copies - learnsite.py rewrites pages the language scripts make, so they go last.
set -e
cd "$(dirname "$0")/.."
python3 tools/lab/build.py
python3 tools/learnsite.py
python3 tools/scenario_blog.py
for l in bengali french spanish arabic; do python3 tools/$l-seo.py; done
python3 tools/site_languages.py # new families must include the language generated last
python3 tools/site_sync.py   # last: the one site bar on every page (the scripts above rewrite pages)
python3 tools/site_cache.py  # hash the final pages, including all new language copies
