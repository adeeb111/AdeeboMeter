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
