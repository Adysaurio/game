# Investigación: diseño de juegos de clase mundial aplicado a Trash Pandas

> 2026-10-08. Enfoque en fuentes recientes (2024–2026); lo clásico solo donde sigue vigente y es la base de lo reciente. Cada sección termina con **lo que aplicamos**.

## 1. Lo que dicen los éxitos recientes (2025–2026)

### PEAK (Aggro Crab + Landfall, GDC 2026: "Putting the Friends in Friendslop")
- **Diseño social primero:** fricción intencional que obliga a cooperar ("no puedes sacar cosas de tu mochila solo: necesitas a un amigo").
- **"Regla Cero: nunca abandones a un amigo en apuros"**, escrita en el juego, con un castigo cómico para quien la rompa.
- **Asimetría de información:** un solo jugador lee la guía y tiene que contársela a los demás. Eso genera conversación y juego de roles.
- **Los muertos siguen jugando:** como fantasmas ven desde arriba y conservan la voz, y siguen siendo "útiles de forma única". No quedan fuera.
- **"El texto es malvado":** comunicar con presencia, voz y gestos, no con texto.
- **Velocidad:** prototipo jugable en 7 días; juego en ~4 meses. Restricciones técnicas convertidas en features (mapa diario).
- Fuentes: [Game Developer: lecciones de friendslop](https://www.gamedeveloper.com/business/peak-co-developer-aggro-crab-shares-lessons-in-friendslop), ["Text is evil"](https://www.gamedeveloper.com/production/-text-is-evil-how-making-peak-changed-indie-studio-aggro-crab), [GDC Vault 2026](https://gdcvault.com/play/1035941/Putting-the-Friends-in-Friendslop).

### Meccha Chameleon (2026: 10 M de copias en 16 días, 2 personas, 2 meses)
- **Un clip funciona si se entiende al instante** sin conocer el juego, **provoca una reacción visible** y **dura 30–60 s**.
- **La pausa antes del descubrimiento** (el cazador se detiene junto al escondido: "espera…") es tensión universal, sin idioma.
- $5.99, cero publicidad: los streamers son el marketing. "Hazlo existir primero, mejóralo después."
- Fuentes: [indiegame.com](https://indiegame.com/en/archives/30041), [AUTOMATON](https://automaton-media.com/en/news/meccha-chameleon-developers-say-the-secret-behind-their-2-month-dev-cycle-is-asset-reuse-and-a-make-it-exist-first-perfect-it-later-approach/).

### R.E.P.O. (2025: 18 M de copias)
- Objetos con física que se cargan con cuidado, incluso entre varios. **Cada monstruo es único** y tiene su propia regla.
- **Primero fue un prototipo de un jugador**; el multijugador vino después. Sincronizar la física fue su mayor reto, y lo resolvieron con mucho trabajo a la medida.
- Fuentes: [Photon blog](https://blog.photonengine.com/r-e-p-o-multiplayer-success-powered-by-photon/), [PC Gamer](https://www.pcgamer.com/games/horror/repo-is-fun-lethal-company-creator-recants-their-criticism-of-the-new-co-op-horror-game-after-trying-to-move-a-grand-piano-through-a-mansion/).

### Lethal Company: monstruos con una regla legible y su contra
- El **Coil-Head** solo se mueve si nadie lo mira. El **Bracken** te ataca si lo miras demasiado. **Las puertas** frenan a los perseguidores.
- Cada amenaza enseña su regla, y la regla tiene una respuesta cooperativa: uno lo mira mientras el otro busca la salida.
- Fuente: [esports.gg](https://esports.gg/guides/gaming/lethal-company-monster-guide).

### Split Fiction (Hazelight, 2025)
- Cada capítulo da **un par de habilidades asimétricas, inútiles por separado**. Nadie puede "cargar" al otro.
- Las mecánicas se **retiran antes de cansar**; el capítulo de los cerdos (pedo-propulsión y resorte) es el ejemplo clásico.
- Fuente: [Xbox Wire](https://news.xbox.com/en-us/2025/03/05/split-fiction-josef-fares-interview/).

### Astro Bot (Team Asobi, GOTY 2024; GDC 2025 y CEDEC 2025)
- **"Cada acción tiene una reacción":** árboles que se sacuden y pasto que brilla al tocarlo. Hasta correr divierte.
- **Un tercio del desarrollo fue prototipado**, con 103 revisiones quincenales del equipo completo.
- **"Una mecánica solo se usa una vez"** por nivel, para que todo se sienta fresco.
- NPCs con reacciones al contexto (se esconden en el peligro y celebran al ganar): el mundo se siente vivo.
- Fuente: [resumen GDC 2025](https://icon-era.com/threads/gdc-2025-the-making-of-astro-bot-astro-bot-took-3-years-of-development-and-was-pitched-to-sony-back-in-may-of-2021.16650/).

### Little Kitty, Big City (2024) y Untitled Goose Game (2019, todavía la referencia del "animal travieso")
- **Juego antes que realismo:** el gato realista "no tenía encanto"; al hacerlo juguetón encontraron el punto.
- **Los humanos de Goose:** cono de visión, memoria de posiciones, globos de pensamiento y animaciones de reacción en cada cambio de estado. **Se rinden después de un rato** y **arreglan el desorden**, para que la travesura se pueda repetir. Esa IA "fácil de abusar" es parte del chiste.
- Fuentes: [GoNintendo](https://www.gonintendo.com/contents/36146-little-kitty-big-city-dev-on-ditching-realism-for-appeal), [Game Developer: Goose Q&A](https://www.gamedeveloper.com/design/behind-the-honk-an-i-untitled-goose-game-i-q-a).

## 2. Persecución y sigilo: cómo se escapa de forma justa
- **Estados escalonados:** casual → alerta casual → sospecha (voltea y mira) → búsqueda → búsqueda agresiva → combate. **Siempre hay un "beat" de reacción**, incluso en el cambio instantáneo a combate. ([Game Design Diary, 2024](https://www.gamedesigndiary.co.uk/post/design-stealth-part-2-ai-behaviours))
- **"El jugador es más fuerte cuando está escondido; hay que dejarlo recuperar esa ventaja si lo detectan."** Los juegos de sigilo débiles no tienen buena fase de huida.
- **Última posición conocida** (Splinter Cell: Conviction): al perderte de vista, buscan donde te vieron por última vez, no donde realmente estás.
- **Fase de evasión** (Metal Gear): pierden el rastro, buscan y luego vuelven a la calma.
- **Pac-Man:** periodos de "dispersión" alternados con persecución, como alivio programado. Cada fantasma persigue distinto: uno te sigue, otro te corta el paso, otro es errático y otro hace lo suyo. ([Pac-Man Dossier](https://www.gamedeveloper.com/game-platforms/feature-the-i-pac-man-i-dossier))
- **Left 4 Dead AI Director:** intensidad por jugador que sube con el daño; picos y luego **relajación obligatoria**. Una montaña rusa, no presión constante. ([Valve/Booth](https://cdn.fastly.steamstatic.com/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf), [Game Developer](https://www.gamedeveloper.com/design/the-discomfort-zone-the-hidden-potential-of-valve-s-ai-director))

**Diagnóstico de nuestro RUN:** los humanos ven a través de todo, nunca te pierden, nunca se rinden y todos persiguen al mismo mapache. No hay escondites que corten la persecución ni respiros. Por eso es imposible escapar.

## 3. Humor mecánico
- **El jugador interpreta el chiste; el diseñador pone el escenario.** La risa sale de la distancia entre lo que intentas y lo que pasa. ([Polaris Game Design, 2025](https://polarisgamedesign.com/2025/mechanical-comedy-in-games/))
- **"Pérdida de control controlada":** sistemas que distorsionan tu intención de forma predecible. **Inversión de estatus:** el fracaso visible es catártico.
- **El punto dulce solo aparece jugando en grupo:** la comedia emergente la descubren los jugadores rompiendo el sistema.
- **No controles el remate:** da un sistema y suficiente agencia para que los jugadores se estorben entre sí.

## 4. Sensación de control ("game feel")
- **Celeste** (código público, la referencia vigente): coyote time de 0.1 s, buffer de salto, salto variable, gravedad a la mitad en la cima del salto, corrección de esquinas, squash & stretch al saltar (0.6 × 1.4) y al aterrizar (hasta 1.6 × 0.4), polvo y rumble en cada acción. ([Player.cs](https://raw.githubusercontent.com/NoelFB/Celeste/master/Source/Player/Player.cs))
- **"Juice it or lose it":** easing, squash & stretch, partículas, screenshake, sonido, y **ponerle ojos a todo**.
- **Astro Bot:** cada toque tiene respuesta.

## 5. Personajes, modelado y rigging (estado 2026)
- **Encanto antes que realismo** (Little Kitty). En los friendslop los personajes son **simples con caras expresivas**: los ojos siguen cosas y la boca se mueve con la voz (PEAK).
- **IA generativa 3D en 2026:** Meshy y Tripo generan personajes estilizados con **auto-rig incluido**; Rodin es para assets de alta calidad; Hunyuan3D es gratis y local (pide GPU potente). Sirven para prototipar y como base para limpiar en Blender, no como producto final. ([comparativa 2026](https://www.krea.ai/blog/best-ai-3d-model-generators-2026))
- **Rigging de cuadrúpedos estilizados:** Auto-Rig Pro es el más completo en Blender (exporta a Unity); Rigify es gratis. IK en patas y FK o spline en columna y cola. En Unity: Animation Rigging (Two-Bone IK) para que las patas pisen el terreno, y huesos con resorte para cola y orejas (movimiento secundario).
- **Voz:** Vivox (de Unity) es gratis para indies y tiene voz posicional por cercanía. Es el factor que **todos** los éxitos comparten.

## 6. Reglas de diseño para Trash Pandas (lo que aplicamos)

1. **El RUN se puede escapar.** Los humanos necesitan verte. Si te pierden, van a donde te vieron por última vez, buscan unos segundos y **se rinden**. Agachado debajo de una mesa o en un seto **cortas la persecución**.
2. **Cada humano persigue distinto**, como los fantasmas de Pac-Man. El jardinero es tenaz pero lento. El mesero es rápido pero se resbala y se cansa. La suegra grita y llama a otros, pero camina. El gato es rápido pero no cabe en todos lados y se espanta.
3. **La presión se reparte:** nunca más de 2 perseguidores sobre el mismo mapache. Los demás buscan a otros o el último lugar visto.
4. **Respiros programados:** cada tanto los perseguidores se detienen ("¿a dónde se fue?", jadean). Es el momento para escapar.
5. **Aventar cosas sirve:** un plato o una copa que le pega a un humano lo aturde. Es la respuesta cooperativa: uno distrae y el otro huye.
6. **"Nunca abandones a un amigo" (Regla Cero de PEAK):** si te atrapan, no quedas fuera. Te meten en una **jaula para mascotas**, y tus amigos pueden **liberarte** (E junto a la jaula). Mientras tanto ves desde arriba y puedes hablar.
7. **El "beat" antes del golpe:** el humano grita y se prepara para golpear con un telegrafiado visible ("!" grande y la escoba levantada) antes de pegar. Así hay tiempo de esquivar.
8. **Cada acción tiene reacción (juice):** squash & stretch del mapache al saltar y aterrizar, polvo, cámara que tiembla en el RUN y al recibir golpes, y una pausa breve (hit-stop) al golpe.
9. **Caras antes que detalle:** cuando hagamos el arte, ojos grandes que miran lo que está cerca, y boca que se mueve con la voz.
10. **Sin texto donde se pueda mostrar:** íconos, flechas y reacciones en lugar de instrucciones largas.
