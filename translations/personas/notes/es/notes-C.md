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
