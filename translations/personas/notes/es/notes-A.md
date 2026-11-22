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
