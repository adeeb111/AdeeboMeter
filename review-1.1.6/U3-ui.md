# U3-ui - release check of 1.1.6 (999a100)

Slice: src/Ui/FullView.Settings.cs, Share, Speed, Sports, SportsPage, Story, Stream, Symptoms, TimePins, ToolFacts, ToolHome, Toolbox, Tools, Top, Topics, Trade, Travel, TryThis, View, Visuals, Visuals3, Visuals4 (7,377 lines, all read). I followed callers into Program.cs (Cfg), Policy.cs, Lang.cs, Weather.cs, Watch/Streams.cs, Watch/StreamCheck.cs, Remote/RemoteKnown.cs and Ui/FullView.Export.cs.
Harnesses (reflection on build6/r6.dll, run with mono): review5/U3-ui/st.cs (Settings rows, language switch, policy locks, search), tb.cs (Toolbox search per language), ts.cs (the SENDS / TAKES facts in tool tips).

### 1. Streaming page reads the live stream's dip list without the lock while the watch thread adds to it
Severity: crash  
Where: src\Ui\FullView.Stream.cs lines 142-162 and 184 (PaintStreamNow); the writer is src\Watch\Streams.cs lines 114, 124, 158 and 183 (`l.S.Dips.Add` under `lock (k)`)  
How to reproduce: play a stream with dips, keep Streaming open, and let a dip close (Streams.Tick → `l.S.Dips.Add(d)`) while the page paints. The paint runs `s.Tally()` (a foreach over Dips), `s.Dips.Last()` and `foreach (var d in s.Dips.Where(...))` after it has left `lock (Streams.k)`, because it copied only `Samples`.  
What happens / what should happen: List<T>.Add during that enumeration throws InvalidOperationException ("Collection was modified") inside OnPaint. DB.OnPaint (Controls.cs line 179) has no catch, so the exception goes to the app's thread-exception handler. The window is small (one dip every few seconds at most), but the page repaints every StreamTick. The fix is already used in the Markets page (Trade.cs line 120, AM-CG-131): copy the session (`Trading.Day.Copy()`) inside the lock, and paint from the copy.  
Confidence: suspected (read it: the writer takes the lock and the reader doesn't)

### 2. Import settings applies a different weather server, the internet check and the phone waiting room without asking
Severity: security  
Where: src\Ui\FullView.Settings.cs lines 660-699 (OutboundKeys and the asks loop in ImportSettings); src\Program.cs lines 482-483 (`wgeo`, `wapi` → Weather.CleanServer); Cfg.Known line 423  
How to reproduce: Settings › Import settings... with a file that contains `wapi=weather.attacker.example`, `wgeo=geo.attacker.example`, `net=15` (your internet check is Off) and `phonestandby=1`.  
What happens / what should happen: the "This file would also:" card lists none of these, and all four load. From then on the weather fetch sends your place's coordinates (your wplace isn't in the file, it's yours) to the file's host every 6 h or 2 days (Weather.ApiHost), and a place search sends the typed name to its geo host. CleanServer accepts any host name, including LAN names. Background pings to 1.1.1.1 restart with no question, and the relay waiting room comes back. The import's own rule (line 681) is that changes which reach a server on their own are asked first. Add `wapi`, `wgeo`, `net` (0 → non-zero), `phonestandby` (0 → 1) and `portal` to the asks. Also, export (line 649) writes `searches` (your recent "Search anything" words), `wgeo` and `wapi`, while its note says only private items are left out. Add them to PrivateKeys.  
Confidence: confirmed (read: each key is in Cfg.Known and in neither OutboundKeys nor the ask branches; Cfg.Read stores them)  
Rule: 1

### 3. Before a run, an HTTP request builder's Authorization header and URL password go into the picture's JSON export
Severity: security  
Where: src\Ui\FullView.Visuals.cs lines 144-151 (VisIn); used by src\Ui\FullView.Export.cs line 305 (Json "inputs") and Visuals4.cs line 244 (the picture's actor name)  
How to reproduce: Toolbox › HTTP request builder. Type `https://admin:pw@api.example.com/x` and the header `Authorization: Bearer abc123`. Don't press Run. Press Ctrl+Shift+S (or Save picture... › Data (JSON)).  
What happens / what should happen: with no result yet, VisIn returns the raw boxes (`Inputs()`), and it blanks only the SecretInput boxes. The JSON's `"inputs"` holds the bearer token and the URL with its password. The picture also shows `admin:pw@api.example.com` as the server's name, because the host regex fails on userinfo and falls back to the whole URL. After a run the same data is redacted (`shown`, Toolbox.cs lines 891-892). Apply that redaction in VisIn too: Reach.NoUser on box 0 and the header regex on box 2 for httpreq.  
Confidence: suspected (read it)  
Rule: 4

### 4. [day 1] After the language is changed while the app runs, Settings' "Privacy & online lookups" page is empty
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs line 76 (`SetSections` is a static readonly array, translated once when the class loads) vs lines 183-187 (the rows' `Sec` is translated each time they are built); FullView.SetHome.cs lines 14-29 (SetQLook keyed by the English name)  
How to reproduce: start in English, then pick Français in the language sheet (or Settings › Language, or Import a file with lang=fr). Open Settings › Confidentialité et recherches en ligne.  
What happens / what should happen: Harness st.cs: `static init lang=en SetSections[10]=Privacy & online lookups` → after the switch, 0 rows match that section, and the rows now say "Confidentialité et recherches en ligne". The page shows only its header, and the home card's summary is empty. The section holds BGP lookups, their timer, the sign-in page check, certificate revocation and IP names. It is the same in the other direction (start fr → switch to en: 0 rows). The page comes back only after a restart. A user who starts in any language other than English also gets this card with the generic gear icon and the section name in place of "What may it ask the internet?" (SetQLookOf falls back). Keep the section key in English, as every other section does, and translate it only when it is drawn.  
Confidence: confirmed (ran it: st.cs, en→fr and fr→en, 0 rows both ways)

### 5. IT policy locks on Settings rows depend on the language, and two rows contradict the policy
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs lines 36-47 (PolicyLocks matches English labels), line 184 (the label is passed through L.T before the lock check), line 171 (Day's numbers), line 40 ("Mode": no such row exists)  
How to reproduce: (a) AdeeboMeter.policy with `online_lookups = off`, language Français: Settings › Confidentialité › "Les désactiver d'eux-mêmes après". (b) The same policy: Streaming & trading › Day's numbers, click On. (c) `mode = simple`: Settings › View › Engineer.  
What happens / what should happen: (a) Harness: under en the BGP row's help is "Set by your IT team"; under fr the timer row shows its normal help and its choices still change Cfg.OnlineMin (fr, es, ar and bn all translate that label). (b) The help says "Off - your IT team turned online lookups off" while the On segment is lit and Cfg.TradeFx is saved as 1. Nothing is fetched (MarketData checks the policy), but the row says two things at once. (c) No row is called "Mode" any more, so the View row is never locked. The switch to Engineer "succeeds", Policy.Apply puts Simple back, and the widget's engineer card then says it "needs Pro mode". Lock by a stable key, not by the shown label. Show the locked state on Day's numbers, and on View when Policy.Mode is set.  
Confidence: confirmed (ran (a): st.cs prints the help of both rows under fr; read (b), (c))

### 6. The "SENDS" and "TAKES" facts in Toolbox tips are wrong for 138 of 232 tools, and the RADIUS lab's tip says nothing leaves the PC
Severity: wrong result  
Where: src\Ui\FullView.ToolFacts.cs lines 13-74 (ToolTakes / ToolSends switch on the old group names "CALC", "KEYS", "SNMP", "LAB", "PATH", "SPEED", "REMOTE", "WATCH"); src\Ui\FullView.Toolbox.cs line 269 (Tools2 replaces each Group with its TbLayout name, e.g. "KEYS & ENCRYPTION", "NETWORK MATHS", "BUILD A PACKET")  
How to reproduce: point at the ? of any of these tools, or press F1 on them. Harness ts.cs prints the facts the tip shows.  
What happens / what should happen: `encrypt / decrypt / keys: SENDS a few small requests to the address you type`; `sla` and 31 other calculators: the same, and TAKES "a few seconds" instead of "answers as you type"; `timeline`: the same, though it only reads history; `snmpwalk`: the generic text, losing "(read only)"; `icmpb`, `udpsend`, `iperf`: "a few small requests", though they send up to 1,000 packets or an iperf3 stream; `phonevs`: "to the address you type". `radiuslab` (group "WHY CAN'T IT JOIN?"): "SENDS nothing outside this PC - a practice server on this PC · TAKES until you stop it", while EngPack.RadiusLab sends an Access-Request with the test password to the server you type and is in SenderGate (Duration says "up to 4 seconds - one Access-Request"). `speedtest`: "100-500 MB to Cloudflare's speed server", while it goes to the server you picked, up to 500 MB. 138 of 232 tools get the fallback text. These tips are how the app keeps its "what leaves your PC" promise tool by tool. Switch on the new group names (or on ids), and correct radiuslab and speedtest.  
Confidence: confirmed (ran it: ts.cs, `tools=232 fallback=138 maths-not-nothing=32`)  
Rule: 1

### 7. An Example's result can be saved, copied and sent from the picture bar
Severity: wrong result  
Where: src\Ui\FullView.Export.cs lines 76 and 173-180 (Save picture... → PNG / .drawio / JSON / CSV), 327 (Copy picture), 395 (Open on my phone), Bluetooth and Encrypt links; reached from every Toolbox tool with a picture (Visuals.cs line 110 sets visRect for Examples too)  
How to reproduce: Toolbox › MTR › Example. Under the picture press Save picture... › Data (CSV), or Ctrl+Shift+C, or Open on my phone.  
What happens / what should happen: Toolbox.cs hides Copy as text, Save... and Compare for an Example (lines 1151-1154, and Ctrl+C in FullView.cs line 356), but the picture bar writes the made-up rows to a file, the clipboard or the phone. Only Show my path and the Time machine link check VisEx(). The JSON says "state": "EXAMPLE", and the CSV keeps the EXAMPLE banner as its first row. The PNG shows the EXAMPLE pill. Rule 9 says Examples are never saved or copied. Hide the bar, or its save/copy/send links, when VisEx().  
Confidence: confirmed (read it; VisualCheck, Visuals.cs line 61-62, runs DrawIo/Json/Csv/PhoneLink on every Example and expects output)  
Rule: 9

### 8. Settings search finds rows by their English names only
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs line 208 (SetMatch searches `Sec + Label + Keys + Help()`; Label and Sec are English, only Help is translated)  
How to reproduce: language Français, search "thème" or "alertes"; Español "tema", "alertas", "clima"; العربية "السمة", "التنبيهات", "الطقس".  
What happens / what should happen: Harness st.cs: all of these give 0 settings. "météo" finds one of the five Weather rows, "historique" finds Notice streams first. "langue" and "idioma" work only because of the Language row's Keys. The rows shown on screen carry the translated names, so search should too: add L.T(Label) and L.T(Sec) to the text. Toolbox search does this already (Toolbox.cs line 962). tb.cs found "pare-feu", "imprimante", "سرعة" and "গতি" correctly. Its only gaps: no accent folding ("securite" → 0) and "cortafuegos" → 0.  
Confidence: confirmed (ran it: st.cs for fr, es, ar)

### 9. Stream Check decrypts the saved links file and reads the history file on every paint; Settings reads phones.txt on every paint
Severity: slow  
Where: src\Ui\FullView.SportsPage.cs line 395 (StreamCheck.Links(): File.ReadAllBytes + ProtectedData.Unprotect) and line 460 (StreamCheck.Earlier(): File.ReadLines); src\Ui\FullView.Sports.cs line 41 (repaints every tick while a check runs); src\Ui\FullView.Settings.cs line 90 (SendingNow → RemotePhones.All() → Store.Read("phones.txt"), called by every PaintSettings)  
How to reproduce: Sports › Stream Check, press Test my stream. The canvas repaints every tick for about 30 s; each repaint does a DPAPI decrypt and two file reads. In Settings, every hover or click repaint reads phones.txt.  
What happens / what should happen: disk and crypto work on the UI thread, repeated without anything changing. Keep the links and the history in memory, and reload them when SaveLink or Save runs. Count the phones once (RemotePhones has the data on load).  
Confidence: suspected (read it)  
Rule: 7

### 10. Stop does nothing during the certificate revocation check
Severity: slow  
Where: src\Ui\FullView.Toolbox.cs lines 509-521 (RowAction "revoke:")  
How to reproduce: HTTPS check with revocation on, press "check revocation" on the row, then press Stop (the Run button now says Stop).  
What happens / what should happen: Toolkit.Revocation(host, port, sni) is never given the Run, so `tbRun0.Stop = true` has no effect. The Toolbox stays busy for up to 10 s, and other clicks only get "still running - press Stop first". Either pass the stop or show no Stop.  
Confidence: suspected (read it)

### 11. Toolbox and Settings text stays English although a translation exists
Severity: cosmetic  
Where: src\Ui\FullView.Toolbox.cs lines 1156-1176 ("Copy as text", "Save...", "Saved ✓", "To my phone", "Compare with the HH:mm run"), line 1185 ("Takes ... Press F1 on anything for what it means."), lines 630-684 (Duration texts), lines 1270 and 1291 (MTU "Next hop >", "Probe all", "Start again", "no answer"), line 1072 (group headers in the All tools list, `grp.Key`); FullView.Settings.cs line 139 ("Show " + name rows); FullView.Story.cs lines 138-198 (box texts)  
How to reproduce: language Français, open any tool and run it.  
What happens / what should happen: lang-fr.txt has "Copy as text → Copier en texte" and "Save... → Enregistrer...", but the bar draws the literal string. The run line, the duration line and the MTU buttons stay English in all four languages, and the All tools group headers are English capitals. Pass each through L.T (measure the translated string).  
Confidence: confirmed (read it; checked with grep against knowledge/lang-fr.txt)

### 12. "Replay bonus · +3 - run it again" is still offered after the bonus was earned
Severity: cosmetic  
Where: src\Ui\FullView.Toolbox.cs line 376 (ShowExample's points note)  
How to reproduce: earn MTR's points, run it again 10+ minutes later (bonus earned), then open its Example.  
What happens / what should happen: for any Replay tool, `Points.Me.Has(id)` always picks the "run it again" text. Check the entry's Again date and say "Already earned" once the bonus has been paid.  
Confidence: suspected (read it)  
Rule: 10 (text only; the award itself refuses a second bonus)

## Still present

| Earlier finding | Status at 999a100 |
|---|---|
| R4 #1 Traffic Lab policy vs iperf3 / bufferbloat / DSCP / WoL | fixed (Toolkit.cs lines 163, 190, 214, 923 check Policy.NoLab) |
| R4 #20 export/import drops voice, revocation, buddy, hat, outage best, overlay | fixed (all in Cfg.Lines and Known) |
| R4 #71 the open window doesn't take up imported settings | fixed (Settings.cs line 728) |
| R4 #73 Toolbox clicks dropped silently while a tool runs | fixed (BusyNote says why) |
| R4 #74 Settings search misses "Wi-Fi", "sign-in", "français" | fixed (st.cs: 5 / 3 / 1 hits); see #8 for other languages |
| R4 #75 Toolbox search "wifi", "SPF", "dmarc" | fixed (tb.cs: roams…, smtppath…, smtppath…) |
| R4 #117 Settings choices spill over the label | fixed (SetRow wraps under the text) |
| R4 #118 safety tick unreadable after a theme switch | fixed (FullView.cs line 682) |

## Checked and held up
- Toolbox secrets: SecretInput boxes are masked, blanked in `shown`, and kept out of ResultText/CSV/the phone; httpreq userinfo, auth headers and body are redacted after a run; IsSecretRow rows are never copied or saved; a result holding a secret row is not kept in tbLast.
- Lab and sender gates: every Senders id is in LabGate or SenderGate; StartTool passes the tick to Lab.Allowed (which checks Policy.NoLab) for both; sweep needs the tick; Points.NoEarn covers the senders.
- Examples: Points.Completed refuses Examples; ShowExample never touches tbLast/tbPrev; Toolbox Copy, Save, Ctrl+C and Compare are hidden for them; FillExample skips password boxes.
- Policy: speed test, Find the closest, the 4K check, Stream Check, Sports, Markets FX, Weather, Online.Start, Packets.Capture / Journey / LiveCapture all honour their switch.
- Settings: every Set persists through Cfg.Save; Cfg.Lines ⊆ Cfg.Known; Read validates each key against its choices; export strips wplace, tradehost, sportteams, addrname, faith; import refuses files over 64 KB or lines over 2,000 characters, asks for autostart, pins, games, broker, history and outbound feeds, and runs Policy.Apply after.
- Threads: every background tool, MTU, key backup/restore, routes, apps, capture and the Sports and Stream callbacks marshal with BeginInvoke and clear their busy flag on InvalidOperationException; SpeedPicker and the menus dispose themselves; the gauge and symptom timers stop when the canvas is hidden or the hover settles; Trade copies its session under the lock.
- Day 1: the speed card ("NOT TESTED YET", "run a test to see"), Stream ("No streams recorded yet"), Sports overview (scores off / no teams), the kick-off history chart, Story with no app connections and TimePins with no history all show words, not 0, NaN or red.
- Toolbox search works in fr, es, ar and bn on translated titles (pare-feu → fw, imprimante/الطابعة/প্রিন্টার → snmpprn, سرعة/গতি → speedtest).
