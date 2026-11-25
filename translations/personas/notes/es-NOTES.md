# Persona translation notes — Spanish (es)

Merged from the per-group notes written by the translator agents. 2,819 lines across 29 persona files.
These are draft translations a native speaker should sanity-check before shipping.

---

## Group A

# Spanish persona translations — notes (Group A: bro, neteng, plain, aristotle, confucius, tao)

Translator: subagent session 01e5620e. All six files are UTF-8 with CRLF line endings
(matching the English sources), trailing newline, headers copied verbatim.

## Voice choices

- **bro** — kept "bro" as the address term throughout: it is the persona's name
  and is widely understood in casual pan-Hispanic speech, so it carries the
  voice better than "colega"/"tío" (both region-marked). Register: short,
  direct, "tranqui", "¡Ey!", "lo clavaste", "modo bestia".
- **neteng** — dry, telegraphic engineer Spanish. Kept all jargon and
  abbreviations untranslated (RTT, RSSI, WLAN, RF, AP, CPE, MTR, PMTUD,
  SYN-ACK, ARP, L2, QoS, SQM, CDN, TTL, ACK/NACK, UI, DF). "Gateway" is
  rendered "puerta de enlace" (the standard term); "gw" kept as "gw" where
  the English uses the abbreviation. "Bufferbloat" kept untranslated (no
  established Spanish term). "Headend" kept as "headend de la VPN".
- **plain** — flat, factual, no colour. Where the English explains a term
  ("This is called bufferbloat"), the explanation survives verbatim in
  Spanish ("Esto se llama bufferbloat").
- **aristotle** — aphoristic, cause-and-effect reasoning, "excelencia",
  "justo medio", "la causa", "por tanto". "Excellence is a habit" ->
  "La excelencia es un hábito". "The agora" -> "El ágora".
- **confucius** — order, duty, correction, courtesy. "Correct it there" ->
  "Corrígela allí"; "Inform them courteously, with the times" ->
  "Avísales con cortesía y con las horas". "To err and correct it is
  learning" -> "Errar y corregirlo es aprender".
- **tao** — water/river/flow imagery throughout: "como el agua quieta",
  "el río es ancho", "bend like the reed" -> "dobla como el junco".
  "It is so" -> "Así es" (kept deliberately minimal).

## Shared decisions

- "Wi-Fi" kept with hyphen and accent-free spelling as in the English;
  "router", "ping", "DNS", "VPN", "stream", "broker", "lag", "jitter",
  "bufferbloat" kept untranslated. "Download" -> "descarga"; "upload"
  does not occur. "Speed test" values ({v}) left as-is.
- "Adeebo Award" -> "Premio Adeebo" everywhere; "Adeebo points" ->
  "puntos Adeebo".
- "goated" (bro speed.great) -> "de otro nivel" (no Spanish equivalent;
  avoids inventing slang). "Beast mode" -> "Modo bestia".
- "fake news" (bro cert.expired) -> "falso" — the catchphrase does not
  exist in Spanish and the fact is "the padlock is not trustworthy".
- "ghosted you" (bro wifi.drops) -> "te dejó tirado".
- "fresh fade" (bro all.good) -> "un corte fresco" (a fresh haircut;
  the barbershop image survives without region-only slang).
- "Rise and shine" (bro morning) -> "Arriba y a brillar".
- "9:30" kept as-is in trade.closed / market lines (neteng keeps "09:30 ET").
- No vosotros forms anywhere; neutral pan-Hispanic register; "tú" used
  only where the English uses "you" directly.

## Lines I was unsure about

1. **bro / wifi.drops** — EN: "Your Wi-Fi isn't a real one, bro. {v} drops."
   ES: "Tu Wi-Fi no es de verdad, bro. {v} caídas."
   Why unsure: "isn't a real one" is a put-down of the Wi-Fi (not the user),
   so it is allowed, but "no es de verdad" is slightly softer than the
   English. Alternative: "Tu Wi-Fi es de mentira, bro."

2. **bro / cert.expired** — EN: "Bro, that padlock is fake news."
   ES: "Bro, ese candado es falso."
   Why unsure: dropped the "news" wordplay entirely; the English is a
   catchphrase-style joke. There is no Spanish equivalent that stays
   under the limits and keeps the fact. Acceptable, but the joke is lost.

3. **neteng / wifi.drops** — EN: "wlan flapped x{v}. Check RSSI and roaming."
   ES: "wlan fluctuó x{v}. Revisa RSSI y roaming."
   Why unsure: "flap" has no single-word Spanish verb in networking use;
   "fluctuó" is the closest neutral term. "Se cayó y se levantó" would be
   clearer but longer and less engineer-like.

4. **neteng / mtu.small** — EN: "Path MTU {v}. Check for a PMTUD blackhole."
   ES: "MTU de ruta {v}. Revisa un agujero negro de PMTUD."
   Why unsure: "agujero negro" is the literal calque used in Spanish
   networking docs, but it is a term of art; a reader unfamiliar with it
   may read it literally. Kept because the English is equally jargon-heavy
   and this is the neteng persona.

5. **aristotle / dns.slow** — EN: "Names resolve slowly ({v}). The fault is in the asking, not the line."
   ES: "Los nombres tardan ({v}). La falta está en la pregunta, no en la línea."
   Why unsure: shortened "resolve slowly" to "tardan" to stay under 16 words
   (the literal version hit exactly 16). The "asking vs line" antithesis
   survives; the "resolve" verb is lost.

6. **aristotle / mtu.small** — EN: "Large packets are lost at {v}. The path's limit is the mean we must respect."
   ES: "Los paquetes grandes se pierden en {v}. El justo medio es el límite de la ruta."
   Why unsure: the literal translation was 18 words, over the limit, so the
   "mean we must respect" phrasing was restructured. "Justo medio" keeps the
   doctrine of the mean; "debemos respetar" was cut for length.

7. **tao / net.none** — EN: "No network. The path is broken; begin where it breaks - the cable or the Wi-Fi."
   ES: "Sin red. El camino está roto; empieza donde se rompe: el cable o el Wi-Fi."
   Why unsure: 15 words, one under the limit; "empieza donde se rompe" is
   slightly repetitive in Spanish ("está roto ... donde se rompe") but it
   mirrors the English's own repetition and keeps the fact.

8. **tao / cert.expired** — EN: "The certificate ended on {v}. All things return to their root."
   ES: "El certificado terminó el {v}. Todo vuelve a su raíz."
   Why unsure: "terminó" is a euphemism for "expired" chosen to fit the
   Taoist voice; the plain fact (expired) is slightly softened. The date
   {v} still carries the fact.

9. **plain / speed.bloat** — EN: "Downloads make the line laggy. This is called bufferbloat."
   ES: "Las descargas hacen que la línea se vuelva lenta. Esto se llama bufferbloat."
   Why unsure: "laggy" -> "se vuelva lenta" is plain and accurate, but
   loses the one-word punch; "con lag" was considered and rejected as
   less plain-register.

10. **confucius / portal** — EN: "This Wi-Fi requires a sign-in first. Observe the custom of the house."
    ES: "Este Wi-Fi exige primero iniciar sesión. Respeta la costumbre de la casa."
    Why unsure: "Observe the custom" -> "Respeta la costumbre" shifts
    "observe" (follow) to "respect"; both fit Confucius, but "sigue la
    costumbre de la casa" is closer to the English verb. Either works.

## Verification

A Python script replicating the Lines.cs self-test (word count, char count,
placeholder set, pipe/tab, emoji range, banned regex) plus key-order and
variant-count comparison against the English files was run on all six
outputs after the CRLF conversion: 576/576 content lines pass, zero errors.
A grep for Spanish banned equivalents (muerte, matar, suicidio, asesin*,
estúpid*, idiota, tonto, perdedor, noob, mierda, joder, puta, guerra,
sangre, bomba, pistola) found nothing.

---

## Group B

# Notes — Spanish translations, group B (epic-fantasy, chill, hype, roast, bestie)

563 content lines translated. All five files pass the app's self-test rules
(≤16 words, ≤110 chars, no emoji, only {v}/{addr}/{Addr}, placeholders preserved,
key order and variant counts exact, header/batch-2 comment lines copied verbatim).

## Voice choices

- **Epic fantasy:** a consistent castle lexicon — castillo, reino, murallas,
  cuervos, estandartes, maestres, heraldos, jinetes, sello, consejo. "Tú"
  imperatives (Cabalga, Arrodíllate). No trademarked names anywhere: no
  Game of Thrones / Star Wars names, places or catchphrases. Where the English
  leans on a GoT-adjacent image I rebuilt it with generic fantasy wording
  (see unsure lines 1–3 below). Dragons, ravens, kings and seals are generic
  fantasy and kept. "Good morrow" became plain "Buenos días" — archaic Spanish
  ("buen día" is modern-neutral; "buenos días" is what a herald would say).
  "The Adeebo Award" → "El Premio Adeebo" (kept "Adeebo" untranslated, as a name).
- **Chill:** short, low-energy, lots of "tranquilo", "con calma", "sin prisa".
  "Smooth sailing" → "Todo tranquilo". "Grab the snacks" → "Prepara las
  palomitas". «lento»/«wifi» use guillemets in search.empty, matching Spanish
  typographic convention.
- **Hype:** exclamation marks throughout, caps only for the English caps
  moments (SUAVE, RÁPIDA, EN VIVO, SEGUIDAS). "champ" → "campeón" (generic
  masculine default; no gender-neutral option fits the line length).
  "Lightning connection" → "Conexión relámpago".
- **Roast:** the gear is always the butt — router, Wi-Fi, proveedor, Windows,
  adaptador — never the user. Dry understatement ("Típico.", "Casi.",
  "Competencia rara.") kept short. "Your router can't say the same" →
  "Tu router no puede decir lo mismo."
- **Bestie:** warm and on the user's side — "Yo me encargo", "No eres tú",
  "Lo haremos juntos", "Te cubro". Deliberately avoided gendered forms:
  "Proud of you" → "¡Qué bien lo haces!" (not "Orgullosa de ti"), and
  "bestie" is dropped in view.switch (no neutral Spanish equivalent;
  "mejor amiga" would gender the buddy). "Yesss" → "¡Siii!", "Obsessed" →
  "Me encanta."

## Register / tech terms

Neutral pan-Hispanic Spanish, no vosotros, no region-only slang. Kept as-is:
Wi-Fi, router, ping, DNS, VPN, MTU, CPU, PC, Bluetooth (n/a here), Windows,
AdeeboMeter, Adeebo Award (as "Premio Adeebo"), 9:30, iperf3 etc. (n/a here).
"Broker" kept as "bróker". "Hop" → "salto". "Download {v}" → "Descarga {v}".
"Stream" → "transmisión". "Lag" kept as "lag" (standard in Spanish gaming
speech). "Speedrunner" → "velocista" in roast, "¡Qué velocidad!" in hype,
"Tranquilo, sigo con la anterior" in chill (the English word itself is
kept nowhere — "speedrunner" has no natural Spanish form).

## Lines I was unsure about

1. epic-fantasy / wifi.drops
   EN: "The signal fell {v} times. The night is dark and full of dropouts."
   ES: "La señal cayó {v} veces. La noche está llena de cortes."
   Why: the English is a play on a famous catchphrase ("the night is dark and
   full of terrors"), which is banned. I deliberately broke the echo instead
   of translating it ("La noche es oscura y está llena de...") — the joke is
   lost, the rule is kept. Flagging in case the joke is worth a different
   rebuild.
2. epic-fantasy / inet.slow
   EN: "Your keep is fine, {addr}. The kingsroad is muddy."
   ES: "Tu fortaleza está bien, {addr}. El gran camino está embarrado."
   Why: "kingsroad" → "camino real" is the published GoT translation of
   Kingsroad, so I used "el gran camino". Same in vpn.slow ("the kingsroad"
   → "el gran camino").
3. epic-fantasy / wifi.drops
   EN: "{v} times today the Wi-Fi bent the knee to the microwave."
   ES: "{v} veces hoy el Wi-Fi se rindió ante el microondas."
   Why: "bent the knee" / "doblar la rodilla" is strongly GoT-associated, so
   I used "se rindió". The surrender joke survives; the phrase does not.
4. epic-fantasy / mtr.lossy
   EN: "Riders vanish at the {v}th tower."
   ES: "Los jinetes desaparecen en la torre {v}."
   Why: Spanish has no "{v}th" ordinal that fits a placeholder cleanly
   ("la torre número {v}" was wordier); plain number keeps the fact.
5. roast / stream.none
   EN: "Nothing streaming. Touch grass?"
   ES: "Nada en transmisión. ¿Sales a tomar aire?"
   Why: "touch grass" has no Spanish idiom equivalent. "Sal a tomar aire"
   keeps the nudge (go outside) but loses the meme bite. Alternative:
   "¿Sal de casa un rato?" — open to a better rendering.
6. roast / wifi.drops
   EN: "Wi-Fi bailed {v} times. Signal strength of a wet noodle."
   ES: "El Wi-Fi se fue {v} veces. Señal con fuerza de fideo mojado."
   Why: kept the "wet noodle" image literally; it is understandable in
   Spanish but may not land as a joke. Alternative: "Señal más floja que
   un fideo cocido."
7. bestie / view.switch
   EN: "{v} it is, bestie. Getting it ready."
   ES: "{v} será. Lo estoy preparando."
   Why: "bestie" dropped — "mejor amiga" genders the buddy, and there is no
   neutral Spanish pet name with the same warmth that fits. Same reasoning
   for sec.ok ("Proud of you" → "¡Qué bien lo haces!").
8. hype / speedrunner
   EN: "Hold up, champ! One at a time!"
   ES: "¡Espera, campeón! ¡Uno a la vez!"
   Why: "campeón" is masculine-default; "campeona" would assume the user's
   gender. No neutral alternative ("¡Espera, crack!" is Spain slang and
   breaks the neutral-register rule).
9. epic-fantasy / award
   EN: "The Adeebo Award. Kneel, and rise a legend."
   ES: "El Premio Adeebo. Arrodíllate y levántate como leyenda."
   Why: kept the kneel/rise image; "levántate como leyenda" is slightly
   compressed vs "rise a legend" but reads naturally and stays in limits.
10. chill / stream.none
    EN: "Nothing playing. I will notice when you hit play."
    ES: "Nada sonando. Notaré cuando le des a reproducir."
    Why: "Nada sonando" is a touch informal ("No hay nada sonando" is
    fuller but longer); kept the short form for the chill voice.

---

## Group C

# Spanish translation notes — group C (noir, pirate, gamer, support, bossy, stoic)

Translator: subagent session 67a7d5e4. Sources: `gpt-drop/.../AdeeboMeter/knowledge/lines-{noir,pirate,gamer,support,bossy,stoic}.txt`.
581 content lines total (105 + 105 + 105 + 112 + 98 + 56). All verified: key order and
variant counts exact, headers verbatim, placeholders preserved, every line ≤16 words and
≤110 chars, no `|`/tab, no emoji, no banned words (English self-test regex + Spanish equivalents).
Worst case: stoic, 14 words / 85 chars. `comfort.txt` untouched. Nothing in the GPT drop or repo was modified.

Note on line endings: I wrote the files with LF endings, but the files on disk are now CRLF — the same
as the English source files (verified: the English sources carry one CR per line). A shared-directory
normalisation step (other translators work in the same folder) converted them; content is unchanged and
re-verified after the conversion. CRLF matches the repo convention for a Windows C# app, so I left it as is.
(Also: `/tmp/verify_es.py` was overwritten mid-task by a parallel translator's script — `/tmp` is shared.
My re-verification used a uniquely named script, `/tmp/verify_es_C67a.py`, and passed.)

## Shared choices

- Neutral pan-Hispanic Spanish, tuteo throughout ("tu", "reinícialo", "dime"), no vosotros, no regional slang.
- Tech terms kept as Spanish speakers actually say them: Wi-Fi, router, ping, DNS, VPN, MTU, ISP, PC, CPU,
  FPS, NAT, bufferbloat, lag, stream, lobby, ranked, GG, AFK, XP, bróker. "Download {v}" rendered as
  "Descarga {v}" / "{v} de descarga" (never "bajada", which is regional). "Hop" rendered as "salto" everywhere.
- "Adeebo Award" → "Premio Adeebo" in all six files (consistent with the award being a proper name).
- "Captive portal" → "Portal cautivo" everywhere; "certificate" → "certificado"; "padlock" → "candado".
- "The provider" → "el proveedor"; "ISP" kept as "ISP" only in gamer (where it fits the register), "proveedor" elsewhere.
- "Rage quit" was never translated literally — gamer uses "abandonar la partida" (no insult, no dark wording).
- No line uses "muerte/morir", "sangre", "guerra", "arma", "bomba", "estúpido/idiota/tonto", "perdedor",
  "noob", profanity, or trademarked names/catchphrases.

## Voice choices per persona

### Noir (hard-boiled detective)
- Register: 1940s detective-film Spanish — "expediente", "coartada", "sospechoso", "testigo", "pista",
  "el rastro", "la ciudad duerme", "caso cerrado". Addressed the user as "amigo" / "detective"
  ("Buen trabajo, detective"; "El jefe se dio cuenta").
- "The trail's gone cold" → "El rastro se enfrió" (kept the cold-trail image without "muerte").
- "Smooth operator" → "Un operador fino" — deliberately flat and dry rather than a song reference.
- "Case file open" → "Expediente abierto"; "papers" (certificates) → "papeles" ("Sus papeles vencieron el {v}").
- "Too clean. I am watching" (stream.on) → "Demasiado limpio. Estoy mirando." — kept the suspicion, dropped nothing.

### Pirate (swaggering sailor)
- Register: nautical Spanish with "¡Argh!", "¡A toda vela!", "camarada" (kept the task's word), "capitán",
  "camarada" as the standard address; "matey" never rendered as "compañero" (too flat) — always "camarada".
- "Doubloons" → "doblones" (standard Spanish doubloon); "crow's nest" → "el mástil" (cofa is correct but
  obscure; mástil reads instantly); "lanterns" (router lights) → "faroles"; "hatch" → "escotilla";
  "overboard" → "por la borda"; "false flag" → "bandera falsa".
- "Parley in progress" → "Parlamento en curso" — the standard Spanish-dub rendering of pirate "parley".
- "Avast!" → "¡Argh!" (there is no good Spanish "avast"; "¡Argh!" carries the same interruption).
- "Yer" dialect was NOT imitated with misspellings — Spanish has no equivalent pirate dialect, so the voice
  comes from vocabulary and exclamations instead of broken spelling.
- "Port {v} be open!" → "¡El puerto {v} está abierto!" (kept the exclamation; {v} is the port number).

### Gamer (gaming slang)
- Register: kept the English gaming loanwords Spanish gamers actually use untranslated: GG, AFK, XP, lag,
  lobby, ranked, build, debuffs, speedrunner, alt-F4, FPS, NAT, jitter, bufferbloat, respawn → "reaparecer".
- "Go carry" → "Ve a por todas" (there is no natural Spanish verb for "carry" a match; "Llevando la partida"
  used for the streak line). "Full send" → "a tope". "Zero lag" → "Cero lag". "Frame drops" → "caídas de FPS".
- "Don't alt-F4" → "No cierres con alt-F4" (kept the joke, in plain Spanish).
- "The server's fine. Your Wi-Fi is throwing the match" → "Tu Wi-Fi te hace perder la partida" —
  "throwing" has no clean Spanish gaming equivalent; this keeps the blame on the Wi-Fi without an insult.
- "Sketchy" (cert.expired) → "Sospechoso" — short, gamer-natural, no slang stretch.
- "One more game?" kept as a question: "¿Una partida más? Vigilo el ping."

### Support (warm, helpful)
- Register: calm second person, reassurance first, blame never on the user: "No es tu equipo",
  "No eres tú", "No es algo que hayas hecho tú", "Lo resolveremos juntos", "Estoy aquí".
- "It's late. I'm watching, so you can rest" → "Es tarde. Vigilo yo, así puedes descansar."
- "Not this time, and that's how learning works" → "Esta vez no, y así es como se aprende."
- "There's a storm nearby; if things wobble, it's not you" → "Hay una tormenta cerca; si algo falla, no eres tú."
- Kept the three-variant keys (app.open, back.again, search.empty, freeze, speedrunner) warm rather than clipped.

### Bossy (bossy, imperative, funny-strict)
- Register: short imperatives, orders aimed at the gear ("Red, compórtate", "Que siga así, router",
  "Nadie pierde un paquete en mi guardia"), minimal politeness words, no insults.
- "Power cycle it" → "Apágalo y enciéndelo de nuevo" (plain instruction; "ciclo de energía" is not idiomatic).
- "Use split tunnelling if allowed" → "Usa túnel dividido si está permitido" (kept the conditional — bossy, not reckless).
- "At ease" (award) → "Descansa" — "¡Descansen!" would be military but too loud for a buddy line; "Descansa" keeps the joke.
- "Dismissed" (mtr.clean) → "Desestimado" — the driest one-word military dismissal that fits.

### Stoic (Marcus Aurelius aphorisms, calm and wise)
- Register: short aphoristic sentences, second-person plural avoided; "lo que está en nuestro poder",
  "no es tu carga", "cada elección tiene su costo", "la prisa no es sabiduría", "actúa, no reacciones".
- No quotations from Marcus Aurelius were used (per the source header); all lines are original aphorism-style.
- "Time is up. It was never ours to keep" → "Se acabó el tiempo. Nunca fue nuestro." — kept the Stoic
  ownership-of-time idea in five words.
- "Everything has its season" (cert.expired) → "Todo tiene su momento" — no death imagery, same acceptance.
- "Wear it lightly" (award) → "Llévalo con ligereza".
- "The market sleeps until 9:30. So may you" → "El mercado duerme hasta las 9:30. Tú también puedes."

## Lines I was unsure about

1. **pirate / call.on — "Parley in progress. Watching the call."**
   English: `Parley in progress. Watching the call.`
   Mine: `Parlamento en curso. Vigilo la llamada.`
   Why unsure: "parlamento" is the Spanish-dub word for pirate parley, but outside that film context it can
   read as "parliament". Alternative: "Tregua en curso" (clearer, less pirate). Kept "Parlamento" for voice;
   swap to "Tregua en curso" if it confuses testers.

2. **noir / speed.great — "Download {v}. Smooth operator."**
   English: `Download {v}. Smooth operator.`
   Mine: `Descarga {v}. Un operador fino.`
   Why unsure: "Smooth operator" is a compliment to the connection, but in Spanish "operador fino" is flat
   and slightly odd. Alternatives considered: "Sin problemas" (loses the noir compliment) or "Todo un profesional"
   (reads like praise for the user, not the line). Kept the literal-ish one; "Todo un profesional" is the fallback.

3. **gamer / game.good — "Ping {v}. Go carry."**
   English: `Ping {v}. Go carry.`
   Mine: `Ping {v}. Ve a por todas.`
   Why unsure: "Ve a por todas" is encouraging but loses the specific "carry the team" meaning; Spanish
   gamers do say "carrear" (borrowed verb) — "Ve a carrear" would be more exact but is slangier and may not
   land for non-gamers. Kept the safer phrase; "Ve a carrear" is the gamer-exact alternative.

4. **pirate / app.open — "Ahoy, {addr}! Lookout's in the crow's nest."**
   English: `Ahoy, {addr}! Lookout's in the crow's nest.`
   Mine: `¡Saludos, {addr}! El vigía está en el mástil.`
   Why unsure: "Ahoy" has no Spanish equivalent ("¡Ah del barco!" is archaic and confusing); "¡Saludos!"
   is plain. "Cofa" is the correct word for crow's nest but obscure; "mástil" is understood by everyone.
   "¡Eh, {addr}!" was considered and rejected as too abrupt.

5. **stoic / mtu.small — "Large packets fall at {v}. Send what the path can carry."**
   English: `Large packets fall at {v}. Send what the path can carry.`
   Mine: `Los paquetes grandes caen en {v}. Envía lo que el camino puede llevar.`
   Why unsure: "{v}" here is the path MTU value, so "caen en {v}" reads slightly odd (they fall *at* a size,
   not *in* it). Alternative: "Los paquetes grandes no pasan de {v}. Envía lo que el camino admite."
   Kept the closer-to-English version; the alternative is arguably clearer Spanish.

6. **bossy / speedrunner — "Queue respected. Last one first."**
   English: `Queue respected. Last one first.`
   Mine: `Cola respetada. El último primero.`
   Why unsure: "El último primero" is a literal rendering that sounds slightly paradoxical in Spanish
   (it means: the previous job finishes before the new one). Alternative: "Primero termina el anterior."
   Kept the terse military-style version for voice; the alternative is clearer.

7. **support / wifi.drops — "Your Wi-Fi has been unsteady. I saved what happened before each drop."**
   English: `Your Wi-Fi has been unsteady. I saved what happened before each drop.`
   Mine: `Tu Wi-Fi ha estado inestable. Guardé lo ocurrido antes de cada caída.`
   Why unsure: "Guardé lo ocurrido" is a faithful translation, but it is vague about *what* was saved
   (the app saves a snapshot). It matches the English vagueness exactly, so I kept it; no better option
   fits the 16-word limit without inventing a feature name.

8. **gamer / inet.slow — "Home setup is clean. The ISP is the lag."**
   English: `Home setup is clean. The ISP is the lag.`
   Mine: `Tu setup en casa está limpio. El ISP es el lag.`
   Why unsure: kept both "setup" and "ISP" untranslated as gamer loanwords; "Tu equipo en casa está bien.
   El proveedor es el lag." would be plainer Spanish but loses the gamer register. Kept the loanwords.

---

## Group D

# Spanish persona translations — notes (Group D: emo, coder, genz, dark-ages, industrial, mission)

Translator: subagent session a3432206. Sources: the GPT drop under
`~/workspace/adeebometer-i18n/gpt-drop/you-will-be-taking-over-adeebometer/AdeeboMeter/knowledge/`
(`lines-emo.txt`, `lines-coder.txt`, `lines-genz.txt`, `lines-dark-ages.txt`,
`lines-industrial.txt`, `lines-mission.txt`) plus the self-test rules in
`src/Lines.cs` lines 210–240. `comfort.txt` was not touched.
All six files verified mechanically against the English originals: 553 content
lines, key order and variant counts identical, every `{v}`/`{addr}`/`{Addr}`
preserved, no `|` or tab inside text, every line ≤16 words and ≤110 chars,
no emoji, no match for the self-test banned-word regex, UTF-8 with trailing
newline, header comment copied verbatim.

## General choices

- Neutral pan-Hispanic Spanish throughout: no vosotros, no region-only slang.
  (One deliberate exception is discussed under dark-ages: "¡Escuchad!" — see below.)
- Tech terms kept as Spanish speakers actually say them: Wi-Fi, router, ping,
  DNS, VPN, MTU, CPU, PC, Bluetooth-style acronyms, and all tool/protocol
  names (HTTP, TLS, TCP, UDP, SSH, NTP, iperf3, dig, MTR, QUIC, DoH, DoT,
  mDNS, SSDP, SMB, BGP) left untranslated. "Download" → "Descarga"/"de bajada";
  "upload" never appears. "Server" → "servidor". "Build" kept as "build" in
  coder (Spanish devs say "build en verde"); "commit", "grep", "changelog",
  "bufferbloat", "lint", "jitter", "hash", "gateway" kept or lightly adapted
  ("gateway" → "gateway" in coder's timeout line; everywhere else "router").
- "Adeebo Award" → "Premio Adeebo" in every persona (a name, translated
  descriptively the same way in all six files for consistency).
- "hop" → "salto" everywhere (MTR/traceroute usage; "salto" is the standard
  Spanish term).
- "PC" kept as "PC" (not "ordenador"/"computadora", except mission's
  "Computadora de a bordo" for "onboard computer", which is the NASA-style
  phrasing).
- No line uses gendered self-reference for the buddy (avoided "despierta/o",
  "segura/o", etc.) so the voice stays gender-neutral.

## Voice choices per persona

### emo — melodramatic, moody, soft
- Sighing, self-aware, quietly dramatic: "Se siente raro, la verdad",
  "Es la señal, no tú", "Todo tiene un costo", "Nada dura".
- "Same, PC. Same." → "Igual que yo, PC. Igual." (kept the stutter-repeat).
- "Patience is a vibe." → "La paciencia es un estado." — "vibe" has no good
  emo-Spanish equivalent here; "un estado" keeps the resigned shrug.
- "The void opened too." (market.open) → "El vacío también." Kept the
  melodrama but stayed light, per the header ("never dark") — no death/dark
  vocabulary anywhere in this file.
- "Wrong. Like everything else." → "Mal. Como todo lo demás." — blunt and
  bleak in the emo register, but not an insult aimed at the user.

### coder — programmer humour
- Jargon kept in English where Spanish devs actually use it: "Hola, mundo",
  "404: internet no encontrado", "build en verde", "tests en verde",
  "Recolectando basura" (garbage collecting), "Async en curso",
  "Bisect apunta ahí", "Lint de seguridad: aprobado", "Handshake TLS limpio".
- "Ship it." → "A producción." (the idiomatic Spanish-dev equivalent).
- "Cache miss city." → "Puro fallo de caché." ("city" as a suffix has no
  Spanish equivalent; kept the meaning and the dry tone.)
- "grep for it: ..." kept verbatim, including the English search terms
  ("slow", "dns", "wifi"), since those are literal search strings.
- "Timeout exceeded." → "Tiempo de espera excedido." (deliberately formal,
  matching the coder's terse style).

### genz — current pan-Hispanic youth slang (dates fast — see warning)
- **Warning: this file dates fastest.** Slang chosen is what is widely used
  across Latin America and Spain in 2026, but several items ("ghostear",
  "W"/"L", "vibras", "protagonista", "modo speedrun") may feel stale within
  a year or two. Deliberately avoided region-only slang: no "güey", "che",
  "vos", "tío", "chido", "guay", "mola", "qué pasada", "la está liando".
- "ghosted you" → "te ghosteó" (Spanglish verb, now pan-Hispanic in youth
  speech); "left you on read" → "te dejó en visto" (universal).
- "Vibes are immaculate" → "Las vibras están impecables"; "vibe check" →
  "prueba de vibras"; "Quiet era" → "Era de calma".
- "Rare W" → "Una W rara"; "Big L for them" → "Una L para ellos";
  "+{v} points. W." → "+{v} puntos. Una W." (W/L kept as letters — they are
  gaming-slang tokens in Spanish youth speech too.)
- "cooked" (PC) → "Tu PC está frita" (widely understood); "mid" → "mediocre";
  "sus" → "sospechoso"; "lowkey elite" → "discretamente élite"; "fr" →
  "en serio"; "the tea" → "el chisme" (pan-Hispanic for gossip).
- "Ate that, no crumbs." → "Lo rompiste, sin dejar nada." ("Lo rompiste" =
  "you killed it", the current pan-Hispanic equivalent; "no crumbs" has no
  Spanish equivalent and was dropped rather than forced.)
- "Nah that's not it, bestie." → "No, eso no es, amigo." ("bestie" has no
  natural Spanish equivalent; "amigo" keeps it warm without inventing slang.)
- "It's giving speedrun." → "Se acabó el tiempo, modo speedrun."
- "Home network ate." → "La red de casa va top." ("va top" is current and
  widely used; "top" as an adjective is pan-Hispanic youth speech.)

### dark-ages — medieval voice (silly, never grim)
- Archaic flavour carried by vocabulary, not by grammar: "¡Escuchad!",
  "pregón/pregonero" (town crier), "pergamino", "escriba", "bufón",
  "heraldo", "por mi fe", "buen señor", "¡Al torneo!", "por palabra del rey".
  The user is addressed as "tú" (modern 2nd person) throughout — no
  "vosotros" forms are used anywhere in the file.
- **One flagged choice:** "app.open|¡Escuchad! La guardia de la red
  comienza, {addr}." uses the vosotros imperative "escuchad" (from English
  "Hark!"). This is a deliberate archaic-theatrical form, not modern
  peninsular usage, and it is the only vosotros-form in the file. If the
  no-vosotros rule is meant to be absolute even for archaic flavour, swap to
  "¡Escucha!" or "¡Atención!". I kept "¡Escuchad!" because "Hark!" needs a
  plural/archaic call and "¡Escucha!" reads as modern, not medieval.
- "Thy" → "tu" (not "vuestro", which would read as Shakespearean/peninsular
  formal and clash with the silly-medieval register).
- "The peasants report no problems." → "Los aldeanos no reportan problemas."
  ("reportan" is Latin-American usage; peninsular would be "informan" — the
  Latin-American form was chosen as the more pan-Hispanic one here.)
- "Witchcraft? No, good fibre." → "¿Brujería? No, buena fibra."
- "Bandits, surely." → "Bandidos, seguro." (kept the joke; "bandidos" is not
  a banned word — no violence is described, just blamed.)
- "Thou art knighted" → "Quedas armado caballero".
- "The candle has burned down." → "La vela se consumió."

### industrial — factory / machine voice
- Steam-age factory floor throughout: "Calderas encendidas", "Girando los
  engranajes", "Generando vapor", "A todo vapor", "medidores", "tuberías",
  "válvula", "correa", "telares", "capataz", "turno", "silbato".
- "The machine runs." → "La máquina funciona."; "Pressure backs up" →
  "La presión sube"; "The works hum along nicely" → "La fábrica zumba
  muy bien."
- "Threw a spanner in" → "metió un palo en la rueda" (the Spanish idiom for
  sabotaging machinery — a closer fit than a literal spanner).
- "The exchange floor is running." (market.open) → "La bolsa está en marcha."
- "Safety inspection" → "Inspección de seguridad"; "safety faults" →
  "fallas de seguridad" (Latin-American "falla"; peninsular "fallo" — "falla"
  chosen as more widely understood across the Americas).

### mission — mission control, calm NASA-style procedure
- Callsign "flight" → "vuelo" throughout ("Vuelo, tenemos luz verde...",
  "Un comando a la vez, vuelo", "Buen trabajo, vuelo").
- "All systems go" / "go" / "no-go" → "luz verde" / "sin luz verde"
  (Spanish mission-control equivalent; "go" left untranslated would read
  as English in the UI).
- "Telemetry nominal" → "Telemetría nominal"; "Downrange" → "lejos" /
  "Lejos." (kept short; "río abajo" would be a mistranslation of the
  range term).
- "Loss of signal" → "Pérdida de señal" (standard Spanish space-ops term).
- "Standby" → "En espera."; "Stand by" → "En espera."
- "Negative. Re-check and retry." → "Negativo. Revisa y reintenta."
- "Clock's at zero." → "El reloj está en cero."
- "Payload" → "Carga" ("Carga cifrada", "Carga abierta").

## Lines I was unsure about

1. **genz — quiz.right** — EN: "Ate that, no crumbs." → ES: "Lo rompiste,
   sin dejar nada." The "no crumbs" half has no Spanish equivalent; the
   translation keeps the praise ("you smashed it") and drops the crumb
   wordplay. Alternative: "Lo hiciste perfecto, sin dejar nada." — slightly
   less slangy. Current choice is fine, but a native Gen-Z reviewer should
   confirm "Lo rompiste" still lands.
2. **genz — speedrunner** — EN: "Speedrun any%? Still on the last one." →
   ES: "¿Speedrun? Aún voy por la anterior." The "any%" joke (a speedrun
   category) was dropped: "¿Speedrun any%?" would be opaque to non-gamers
   in Spanish and the line is already slang-dense. "Speedrun" itself is kept
   as a loanword (widely used in Spanish gaming speech). If the "any%" gag
   matters, "¿Speedrun any%? Aún voy por la anterior." also fits the limits
   (7 words, 42 chars).
3. **dark-ages — app.open ("Hark!")** — see the "¡Escuchad!" note above. It
   is the only vosotros-form in the whole delivery. Swap to "¡Escucha!" or
   "¡Atención!" if the rule is absolute.
4. **emo — speedrunner** — EN: "Patience is a vibe." → ES: "La paciencia
   es un estado." "Vibe" is untranslatable here without sounding forced
   ("una vibra" would be wrong in emo register). "Un estado" (a state of
   mind) keeps the resigned tone. Alternative: "La paciencia es todo un
   tema." — arguably closer to the shrug; current choice is acceptable.
5. **coder — dns.slow** — EN: "Cache miss city." → ES: "Puro fallo de
   caché." The English is a joke suffix ("X city" = "lots of X"); no Spanish
   equivalent exists, so the joke was replaced with a flat equivalent. A
   Spanish dev would more likely say "Caché: todo fallos." — either works.
6. **industrial — sec.issue** — EN: "{v} safety faults flagged." → ES:
   "{v} fallas de seguridad marcadas." "Falla" (LatAm) vs "fallo" (Spain)
   was a coin toss; "falla" chosen as more widely understood. Same choice
   in coder's "Puro fallo de caché" — note the inconsistency is deliberate
   (coder uses the Spain-favoured "fallo" because Spanish devs write
   "fallo de caché"; industrial uses LatAm "falla"). Flagging in case a
   single convention is preferred.
7. **mission — game.good** — EN: "Link stable. You are go." → ES: "Enlace
   estable. Tienes luz verde." "You are go" is a callsign-style clearance;
   "Tienes luz verde" is the natural Spanish rendering, but it loses the
   "you are go" phrasing. Alternative: "Enlace estable. Luz verde para ti."
8. **genz — all.good** — EN: "No problems. Your setup is lowkey elite." →
   ES: "Sin problemas. Tu setup es discretamente élite." "Lowkey" has no
   good Spanish slang equivalent; "discretamente" is correct but flatter
   than the English. "En secreto" was considered and rejected (wrong
   meaning). Acceptable as-is.
9. **dark-ages — pc.busy** — EN: "The PC labours like an ox." → ES: "La PC
   trabaja como un buey." "Buey" is correct and pan-Hispanic, but the image
   is slightly rustic rather than medieval-court. Alternative: "La PC tira
   como un buey de carga." — current choice is shorter and fine.
10. **emo — market.open** — EN: "The markets opened. The void opened too."
    → ES: "Los mercados abrieron. El vacío también." Kept the void joke
    because the English header says "never dark" and the line is already in
    the source; "vacío" is abstract/melodramatic, not a banned topic. If
    even this is too dark, "Los mercados abrieron. El resto, también." is a
    safe fallback.

## Verification performed

A Python check (replicating the `Lines.cs` self-test: word count, char
count, placeholder allowlist, banned-word regex, emoji/surrogate scan) plus
key-order/variant-count comparison against the English originals was run on
all six files: **0 problems, 553/553 content lines**. A separate grep sweep
confirmed no vosotros forms (other than the flagged dark-ages "¡Escuchad!"),
no profanity, and no region-only slang in the six files. (One grep hit on
"puta" inside mission's "ComPUTAdora" is a substring false positive — the
word is "computadora".)

---

## Group E

# Notes — Spanish persona translations, group E (nature, poetic, revolution, scientist, shakespeare, space-saga)

Translator notes for the six files in this group. All 546 content lines (91 per file)
pass a Python re-implementation of the `Lines.cs` self-test: key order and variant
counts identical to the English files, placeholders preserved exactly, no `|` or tab
inside text, max 12 words / 68 characters per line (limits are 16 / 110), no emoji,
no banned-regex hit, headers copied verbatim, UTF-8 with trailing newline.

## Voice choices

### Nature (`lines-nature-es.txt`)
- Whispered documentary narrator: present tense, short observational sentences,
  the network as a habitat ("hábitat", "manada", "guarida", "territorio").
- Animal images kept close to the English: perezoso (sloth), halcón (falcon),
  guepardo (cheetah), manada (herd/pack).
- "hop" (MTR) rendered as "salto" throughout the group for consistency.

### Poetic (`lines-poetic-es.txt`)
- Lyrical, image-led, intimate "tu": water, light and road images ("Aguas
  quietas, cielo despejado", "el hilo está cortado", "Tras tu puerta, el
  camino se alarga").
- Deliberately the softest register of the six; short fragments allowed
  ("Sin grietas.", "Cuento {v}.") to keep the musical brevity of the English.

### Revolution (`lines-revolution-es.txt`)
- Rallying but strictly civic and non-violent, per the English header: the
  vocabulary is "ciudadano / compañero / pueblo / asamblea / consejo / causa /
  acta" — no war, death or weapon language anywhere.
- "the struggle continues" becomes "¡la lucha continúa!" — "lucha" here is
  civic struggle (as in "lucha por los derechos"), not combat.
- "barricade" was NOT translated as "barricada" (street-fighting overtone);
  it became "puesto de guardia" ("Todo tranquilo en el puesto de guardia").
- "Burning the midnight oil" became the plain "Trabajando hasta tarde" —
  there is no natural Spanish idiom that fits the word limit without
  sounding forced.

### Scientist (`lines-scientist-es.txt`)
- Precise, curious, lab-register: "Hipótesis", "Variable aislada", "Prueba de
  control", "Tamaño de muestra", "Revisado por pares".
- Technical terms kept as Spanish speakers say them: "Bufferbloat" left
  untranslated (it is the standard term, as in English), "jitter" kept,
  "portal cautivo" for captive portal, "puerta de enlace" for gateway.
- "Zero data points" shortened to "Cero datos" to stay inside the word limit
  while keeping the meaning.

### Shakespeare (`lines-shakespeare-es.txt`) — the hard one
- Golden-Age Spanish flavour, not parody and not a translation of
  Shakespeare's plays (no quotations anywhere, per the English header).
- Second person is consistently **vos** with classical verb forms, never
  modern "tú" and never "vosotros": "Hablad", "Nombrad", "Acercaos",
  "Soltad", "Reiniciadlo", "Habéis vuelto", "Estáis en consejo",
  "Descansad; yo velo", "Quedaos un rato", "Registraos, antes de pasar".
  Possessives are "vuestro / vuestra / vuestros" throughout.
- Archaic vocabulary used sparingly and only where still understandable:
  "mas" for "but" ("Veloz, mas con carga tropieza {v}"), "mora" for
  dwells/lives ("La lentitud mora más allá", "{v} dispositivos moran en
  vuestra red"), "presto, presto" for "Anon, anon", "acaecieron" for
  "befell", "He aquí el relato", "Honor a vos".
- Greeting "Good morrow" is "Dios os guarde, {addr}" (period greeting) and
  "Good morrow" in the morning lines is plain "Buenos días" — the archaic
  greeting twice would feel like a tic.
- "Go forth" is "Id adelante" (archaic imperative of "ir").
- "Thy provider is gone dark" is "Vuestro proveedor quedó a oscuras"
  (avoided "ha caído" here because the line before already uses "ha caído"
  for the internet; variety, not a rule).
- Deliberate check: "Nadie sino vuestro amigo podrá leerlo" contains the
  substring "die" inside "Nadie", but the C# banned regex requires a word
  boundary before "die" (`\bdie\b`), so it does not match — verified by
  running the regex itself (0 hits across all six files).

### Space saga (`lines-space-saga-es.txt`) — the other hard one
- Generic space opera, no film names or catchphrases: fleet/bridge/relay/
  beacon/hangar/hull vocabulary ("La flota se mantiene", "De vuelta en el
  puente", "Campo de asteroides", "Tormenta de iones").
- No "warp", "hyperdrive" brand language: "Velocidad hiperespacial" and
  "Motor de salto" are generic; "hyperspace speed" is a plain descriptive
  phrase, not a franchise term.
- "Cloaking device" is "Dispositivo de ocultación" (descriptive, not a
  franchise name); "Holo-llamada" is a coined compound for "Holo-call" —
  short, transparent, and inside the limits ("videollamada holográfica"
  would have blown the word budget on those lines).
- Kept the pilot address ("Con calma, piloto", "Tu rango sube, piloto")
  and the ship-as-home framing ("Tu nave está bien", "Baliza de casa").

## Shared decisions
- "hop" → "salto" everywhere; "packet loss" → "pérdida de paquetes";
  "checkup" → "revisión"; "award" → "Premio Adeebo" (kept "Adeebo Award"
  untranslated nowhere — the English files say "the Adeebo Award" and the
  Spanish renders it as "el Premio Adeebo", which reads naturally; "Adeebo"
  itself is never translated).
- "AdeeboMeter", "Windows", tool/protocol names (DNS, VPN, MTU, CPU, ping
  as "Ping"), acronyms and file names were never translated.
- Neutral pan-Hispanic Spanish: no "vosotros", no regional slang.
  ("Vos" appears ONLY in the Shakespeare persona, as an archaic literary
  form, not as Rioplatense voseo — its verbs are the classical "hablad /
  sois" forms, not "hablás / sos".)
- "Wi-Fi" keeps its hyphen and capitalisation; "router", "lag", "ping"
  kept as Spanish speakers use them.

## Lines I was unsure about

1. `nature / portal` — EN: "This watering hole requires a sign-in."
   ES: "Esta fuente exige iniciar sesión."
   Why unsure: first draft was "Este abrevadero exige iniciar sesión",
   which is the natural image — but "abrevadero" contains the substring
   "vader", and the C# banned regex lists `vader` WITHOUT word boundaries,
   so the self-test would flag it. "Fuente" (spring / water source) keeps
   the watering-place image and passes. Flagging this because it is a real
   self-test trap, not a style preference: any future edit reintroducing
   "abrevadero" will fail the check.
2. `space-saga / idle` (2nd variant) — EN: "Long-range scan: nothing new."
   ES: "Exploración de largo alcance: nada nuevo."
   Why unsure: my first draft accidentally left the English half in place
   ("Long-range scan: nada nuevo."). Caught and fixed before verification;
   listed here so a reviewer knows to look at it.
3. `shakespeare / busy` (3rd variant) — EN: "Anon, anon."
   ES: "Presto, presto."
   Why unsure: "Anon" (soon / presently) has no exact Golden-Age twin;
   "presto" (quickly / at once) shifts the meaning slightly toward
   immediacy. Alternative considered: "Enseguida, enseguida" — more
   accurate, but flat and un-period. Kept "presto" for flavour.
4. `shakespeare / vpn.slow` (1st variant) — EN: "The masque costs {v}."
   ES: "El disfraz cuesta {v}."
   Why unsure: "masque" (masked ball / disguise) is doing double duty in
   the English as a period-flavoured word for the VPN disguise. Spanish
   "mascarada" would match the period flavour better, but "El disfraz
   cuesta {v}" is clearer and shorter; kept the plain word.
5. `poetic / update.suspect` (2nd variant) — EN: "Since {v}'s update, a shadow."
   ES: "Desde la actualización de {v}, una sombra."
   Why unsure: it is a fragment in both languages, and {v} is presumably a
   version or date, so "de {v}" is the right preposition — but if {v} ever
   renders as a full date string the line reads slightly oddly in both
   languages. Mirrors the English exactly, so left as is.
6. `revolution / quiz.wrong` — EN: "Wrong - the struggle continues!"
   ES: "Incorrecto: ¡la lucha continúa!"
   Why unsure: see voice notes above — "lucha" is civic struggle, but a
   reviewer sensitive to the "never violent" rule should confirm this
   reading. If in doubt, the safe swap is "¡la causa continúa!".
7. `nature / port.closed` (2nd variant) — EN: "No sign of life at {v}."
   ES: "Sin señales de vida en {v}."
   Why unsure: "vida" (life) is not a banned topic and the English says
   the same thing, but it is the closest any line in this group comes to
   the banned-topic list. Kept as a faithful translation.
8. `space-saga / call.on` (1st variant) — EN: "Holo-call active. Monitoring signal."
   ES: "Holo-llamada activa. Vigilando la señal."
   Why unsure: "Holo-llamada" is a coined compound (see voice notes).
   It is transparent, but if the project prefers no coinages, the swap is
   "Llamada holográfica activa. Vigilando la señal." (8 words, fits).
9. `shakespeare / morning` (2nd variant) — EN: "Dawn, and naught amiss."
   ES: "Amanece, y nada anda mal."
   Why unsure: "naught amiss" is compact period English; "nada anda mal"
   is plain modern Spanish and loses the archaic lift. "Y nada fuera de
   orden" was considered and rejected as clunky. Acceptable, but the
   weakest Shakespeare line in the set.
10. `scientist / speed.bloat` (1st variant) — EN: "Under load, latency increases {v}. Bufferbloat."
    ES: "Bajo carga, la latencia aumenta {v}. Bufferbloat."
    Why unsure: kept "Bufferbloat" untranslated as the standard technical
    term. If the project wants a gloss, "retención en búfer" exists but is
    rarely used by Spanish-speaking network engineers and would read as a
    mistranslation.

## Verification performed
- Python re-implementation of the `Lines.cs` `Check()` rules (word count,
  110-char limit, emoji/surrogate scan, placeholder allow-list, the exact
  banned regex from `Lines.cs` lines 217–218) plus: header verbatim, content
  line counts, key order, per-key variant counts, `|`/tab scan, trailing
  newline, UTF-8.
- Result: 546 content lines, 0 problems. Longest line 68 characters
  (space-saga), most words in any line 12 (nature, poetic).
- `comfort.txt` was not read, opened or modified.
- Sources were not modified; only the six output files and this note were written.
- Note: a sibling agent's script briefly overwrote a shared `/tmp/verify_es.py`
  while this task ran; verification was re-run from a uniquely named script
  (`/tmp/verify_es_groupE_d8278b4e.py`) and the result above is from that run.
