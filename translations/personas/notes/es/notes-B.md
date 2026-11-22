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
