#!/bin/sh
# Rebuild the website after changing pages, in the order that matters (21 Nov): the lab page, the Learn pages and the
# offline cache first, then the language copies - learnsite.py rewrites pages the language scripts make, so they go last.
set -e
cd "$(dirname "$0")/.."
python3 tools/lab/build.py
python3 tools/learnsite.py
for l in bengali french spanish arabic; do python3 tools/$l-seo.py; done
