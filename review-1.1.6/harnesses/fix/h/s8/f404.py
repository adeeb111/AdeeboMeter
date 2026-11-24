from playwright.sync_api import sync_playwright
with sync_playwright() as p:
    br=p.chromium.launch(executable_path="/usr/bin/google-chrome", args=["--no-sandbox"])
    pg=br.new_page(); reqs=[]
    pg.on("response", lambda r: reqs.append((r.status,r.url)))
    pg.goto("http://127.0.0.1:8765/index.html"); pg.wait_for_timeout(1500)
    for s,u in reqs:
        if s>=400 or not u.startswith("http://127.0.0.1"): print(s,u)
    br.close()
