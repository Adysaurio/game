# Trash Pandas in a Trenchcoat — Documento de diseño

> Nombre provisional. Fecha: 2026-10-07. Estado: borrador para revisión.

## 1. Intención

**Objetivo:** un juego multijugador en línea, comercial en Steam (precio objetivo $5–8 USD), gracioso, con partidas cortas y adictivas que generen clips para compartir.

**Quién lo hace:** una persona con background en diseño de producto, motion graphics y fundamentos de 3D, sin experiencia programando juegos. Claude escribe la mayor parte del código y arma el proyecto; el arte se produce con Higgsfield y Blender.

**Éxito del prototipo:** en cada playtest de 10 minutos surge al menos un momento que los jugadores quieren compartir ("la prueba del clip"), y los jugadores piden "otra ronda".

### La fórmula (derivada del análisis de Burglin' Gnomes, Meccha Chameleon y Mimic Party)

1. Se entiende en un clip de 5 segundos.
2. Fallar es tan gracioso como ganar.
3. Rondas de 2–5 minutos con ganas de "otra más".
4. El jugador *crea* el momento; no el contenido.
5. Factible para una persona: red sencilla, poca física sincronizada.
6. Pulido con estilo estilizado, no realista.

### Diferenciación

La idea de "varios jugadores controlan un solo cuerpo disfrazado" ya existe (*Out On A Limb*, co-op de *Octodad*, *Struggling*). Lo que nos distingue:

- **Separarse y re-apilarse:** los mapaches pueden salir de la gabardina para actuar solos y deben volver; el orden de la pila define los roles.
- **Avaricia vs. equipo:** cada mapache acumula botín personal, pero solo cobra si escapa. Salirse a robar debilita el disfraz.
- **Dos fases con sensaciones opuestas:** infiltración (tensión y coordinación) y pánico (caos y cada quien por su cuenta).

## 2. Concepto

De 2 a 5 mapaches apilados dentro de una gabardina se hacen pasar por un señor elegante para robar en una boda en un jardín. Los humanos no notan nada… pero el gato sí.

## 3. Loop de una ronda (≈3–5 min)

1. **Lobby / guarida** → los jugadores eligen cosméticos y utensilio y arrancan.
2. **Infiltración:** la gabardina entra a la boda. Cumplen objetivos y roban botín, evitando que la sospecha llegue al tope.
3. **Fin de infiltración**, por una de dos vías:
   - **Salida limpia:** la gabardina sale por la puerta principal sin ser descubierta → todos escapan y reciben **multiplicador de botín**.
   - **"¡¡RUUUN!!":** la sospecha llega al 100 %, un humano grita, la gabardina explota (cámara lenta) y empieza el pánico.
4. **Pánico:** cada mapache, con su propia cámara, corre a una de las 4 salidas. Los humanos persiguen y atacan (escobas, sartenes, sillas). El gato es el cazador más rápido.
   - Mapache que escapa → cobra su botín.
   - Mapache atrapado o aplastado → pierde su botín y pasa a espectador (animación cómica).
5. **Resultados:** botín por jugador, objetivos cumplidos, momentos destacados → regreso a la guarida.

La ronda tiene un **tiempo límite de infiltración** (valor inicial: 4 min); al agotarse, la sospecha sube rápidamente hasta forzar el "¡RUN!" si no salieron antes.

## 4. La gabardina

Un solo personaje con animación procedural + física ligera (se tambalea, se ve torpe, pero sigue siendo controlable). Simulado únicamente en el host.

### Reparto de puestos según número de jugadores

| Jugadores | Puestos |
|---|---|
| 2 | Abajo (piernas) · Arriba (brazos + cabeza) |
| 3 | Piernas · Brazos · Cabeza |
| 4 | Pierna izq. · Pierna der. · Brazos · Cabeza |
| 5 | Pierna izq. · Pierna der. · Brazo izq. · Brazo der. · Cabeza |

Más jugadores = más objetivos en el mapa y un multiplicador de botín por tamaño de equipo, para compensar la dificultad extra.

### Controles (teclado/mouse y control)

> Revisado el 2026-10-07 tras el primer playtest gris. Ver `docs/research/2026-10-07-movement-and-camera-feel.md`.

**Principio:** cada rol, por sí solo, se siente como un juego en tercera persona normal y preciso. La comedia sale de la **descoordinación entre jugadores**, nunca de controles malos (lección del co-op de *Octodad*). Lo torpe del señor es visual y secundario (lección de *Fall Guys*).

Cada jugador **orbita su propia cámara con el mouse**. Todas siguen al señor.

- **Piernas:** WASD camina **relativo a tu cámara**; el señor gira suave hacia donde camina. Espacio salta (coyote time 0.12 s, jump buffer 0.15 s), Ctrl agacha. Con piernas separadas, cada pierna empuja hacia la dirección de *su* jugador: si no coinciden, el señor avanza menos y se tuerce.
- **Brazos** (estilo *Human: Fall Flat*): **las manos apuntan a donde mira tu cámara**. Clic izquierdo y derecho estiran y agarran con la mano izquierda y derecha.
- **Cabeza:** la cabeza mira hacia donde mira tu cámara (dentro de un rango natural). Responde conversaciones y marca con *ping*.
- **Mapache suelto:** tercera persona estándar: WASD relativo a la cámara, el mapache mira hacia donde corre, salto con coyote y buffer, gravedad de caída más fuerte, salto de altura variable.

### Cámara

- Cámara orbital por jugador (Cinemachine 3: `CinemachineCamera` + `OrbitalFollow`), con damping solo en posición y colisión con el escenario.
- Input del mouse crudo, sin suavizado. El cursor se bloquea una sola vez y se filtran los picos del bug de macOS.
- Rigidbody interpolado; la cámara se actualiza después de la física.

### Salirse y volver

- `E` sale de la gabardina; el puesto queda **vacío** y la función se pierde:
  - sin piernas → el señor se arrastra o se sienta de golpe;
  - sin brazos → las mangas cuelgan;
  - sin cabeza → la cabeza se ladea y no puede contestar.
- Para volver: acercarse al señor y presionar `E`. Se entra a cualquier puesto libre (permite reacomodarse).

### Mapache suelto

Corre, **salta**, **trepa** (cortinas, manteles, el seto), se mete **debajo de las mesas** y se cuela por huecos. Puede cargar un objeto de botín a la vez (en la boca).

## 5. Sospecha

Medidor global de 0 a 100.

**Sube por:**
- puestos vacíos (por segundo, por cada función faltante);
- movimientos raros: caídas, giros, chocar con cosas o personas;
- el gato cerca del señor (le bufa);
- respuestas de diálogo fallidas o fuera de tiempo;
- **un humano ve a un mapache suelto** (subida grande).

**Baja:** lentamente mientras el señor se comporta normal (todos los puestos ocupados, sin eventos raros).

Al llegar a 100 → "¡¡RUUUN!!". Los valores concretos se definen y ajustan en playtest; el sistema debe exponerlos como parámetros configurables.

## 6. Nivel 1: boda en el jardín

### Zonas

- **Entrada:** arco de flores y anfitrión que saluda (primer diálogo obligatorio). Es también la salida limpia.
- **Mesas de invitados:** gente sentada, centros de mesa, carteras colgando: botín fácil.
- **Mesa de regalos:** sobres y cajas envueltas.
- **Pastel de varios pisos:** objetivo estrella, requiere coordinación.
- **Pista de baile / gazebo:** cuando suena la música, el señor debe bailar o sube la sospecha.
- **Carpa del catering:** meseros que entran y salen con charolas.

### Personajes no jugables

- **Invitados:** estados tranquilo → curioso → alarmado → pánico. En pánico huyen, se suben a las mesas o atacan a los mapaches cercanos.
- **Mesero:** ofrece bebidas al señor, lo que dispara un diálogo y obliga a los brazos a tomar la copa sin tirarla.
- **El gato:** patrulla; si se acerca al señor lo huele y bufa (sospecha sube rápido y los humanos voltean). Se le puede distraer lanzándole comida. En pánico es el cazador más rápido.

### Objetivos especiales (prototipo: 3)

- El anillo de la novia.
- El muñequito de la cima del pastel.
- El sobre más gordo de la mesa de regalos.

Los objetivos dan mucho más botín que los objetos sueltos y se exhiben como trofeos en la guarida. Al menos uno requiere coordinación de la gabardina (p. ej., subir al pastel: las piernas saltan y los brazos se cuelgan en el mismo instante).

### Salidas para el pánico (4)

Hueco en el seto · alcantarilla del jardín · camioneta del catering · estanque/fuente.

## 6b. Eventos sociales (el ritmo de la infiltración)

> Agregado el 2026-10-07 a partir de una idea del usuario: le dan ritmo a la ronda (calma → alarma → carrera de regreso → tensión → alivio o desastre) y resuelven que la cabeza se aburra.

1. **Disparo:** al azar durante la infiltración (no a intervalos fijos), sin repetir NPC dos veces seguidas.
2. **Aviso:** pantalla "¡EVENTO EN N…!" con el retrato del NPC (mesero, suegra, novia, cura…) y un indicador de dirección. **El NPC camina de verdad hacia el señor** desde un punto lejano.
3. **Duración del aviso (dinámica):** `clamp(distancia del mapache más lejano a la gabardina / velocidad del mapache + 3 s, 8 s, 20 s)`. El punto de partida del NPC se elige para que su caminata dure ese tiempo. Así el aviso es justo aunque el mapa crezca.
4. **¡Todos a sus puestos!:** los mapaches sueltos deben regresar y el señor debe verse normal (completo, de pie, quieto) cuando el NPC llegue.
5. **Cámara de evento:** la cámara de todos se acerca a la escena (señor + NPC).
6. **Cada rol tiene su parte, con tiempo límite:**
   - **Cabeza:** elige la respuesta de diálogo (ver §7). Es su momento protagonista.
   - **Brazos:** la acción que pide la situación (saludar de mano, tomar la copa que ofrecen, aplaudir).
   - **Piernas:** la acción que pide la situación (quedarse quietas, pasito de baile, sentarse).
   - Con menos jugadores, cada puesto combinado hace las partes de sus funciones.
7. **Resultado:** éxito = la sospecha baja un poco. **Cualquier parte fallida o un puesto vacío (alguien no llegó) = la sospecha sube mucho.**

**Entre eventos, la cabeza es el actor y vigía:** reacciona con expresiones a momentos de la boda, ve el radar de sospecha y avisa al equipo, puede **distraer** a los humanos cercanos con recarga ("¡Miren, el novio!", piropo, brindis) para abrir ventanas de robo, y debe aguantar el **estornudo** cuando el gato se acerca (si falla, se le vuela el sombrero). *Descartado:* robar con la boca (duplicaba a los brazos).

## 7. Diálogos

Cuando un humano le habla al señor, la cabeza ve 3 respuestas en burbuja con tiempo límite (≈4 s). Una es correcta, otra es neutral y otra es absurda (incluido "idioma mapache"). Una respuesta incorrecta o un silencio suben la sospecha; una absurda puede provocar una reacción cómica del NPC. Si el puesto de cabeza está vacío, el diálogo cuenta como silencio.

## 8. Progresión: la guarida

Lobby y menú en un basurero. Persistente entre partidas.

- **Botín acumulado:** moneda para desbloquear.
- **Cosméticos (prototipo: ~8):** gabardinas, sombreros, bigotes falsos, lentes, apariencias del mapache.
- **Utensilios (prototipo: 2):** un espacio por mapache, de uso principal en el pánico.
  - *Cáscara de plátano:* hace resbalar a un perseguidor.
  - *Bomba de humo:* bloquea la visión en un área y te da un momento para escapar.
- **Trofeos:** los objetivos especiales robados se exhiben en la guarida.

## 9. Arquitectura técnica

### Motor y herramientas

- **Unity 6** con URP. Unity Personal (gratis bajo $200k USD de ingresos anuales).
- **Integración con Claude:** servidor MCP del Unity CLI y plugin oficial de Unity para Claude Code (gratis; no requieren créditos de Unity AI).
- **Control de versiones:** git + Git LFS para binarios (modelos, texturas, audio).
- **Arte:** Higgsfield (conceptos y modelos base) → Blender (limpieza, rig, animación) → FBX/glTF → Unity.

### Red

- **Netcode for GameObjects** (oficial de Unity) con un transporte de Steam.
- **Steamworks:** lobbies, invitaciones a amigos, paso por NAT, Steam Cloud.
- **Modelo host-autoritativo:** sin servidores dedicados (costo de operación $0).
  - El host simula la gabardina, los NPCs, el gato, la sospecha, el botín y la ronda.
  - Los clientes envían inputs de su puesto y reciben el estado; la gabardina se interpola en los clientes.
  - Mapache suelto: predicción del lado del cliente dueño, validada por el host.

### Sistemas (unidades independientes)

| Sistema | Responsabilidad | Depende de |
|---|---|---|
| `RoundManager` | Fases: lobby → infiltración → pánico → resultados; temporizador | Sospecha, Botín |
| `TrenchcoatBody` | Cuerpo físico/procedural del señor; combina inputs de cada puesto | Puestos |
| `SlotSystem` | Asigna puestos según número de jugadores; entrar/salir; puestos vacíos | — |
| `RaccoonController` | Movimiento individual: correr, saltar, trepar, esconderse, cargar | — |
| `SuspicionSystem` | Medidor global; recibe eventos y aplica subidas y bajadas | — |
| `NpcBrain` (+ `Guest`, `Waiter`, `Cat`) | Máquinas de estado; percepción de rarezas; pánico y persecución | Sospecha |
| `DialogueSystem` | Conversaciones cronometradas de la cabeza | Puestos, Sospecha |
| `LootSystem` | Objetos, objetivos, quién carga qué, cobro al escapar | — |
| `ProgressionSystem` | Guarida, desbloqueos y guardado (Steam Cloud + respaldo local) | Botín |
| `NetworkSession` | Steam lobby, conexión, host/cliente, desconexiones | — |

Los sistemas se comunican por eventos (p. ej., `SuspicionSystem` escucha "puesto vacío", "humano vio mapache", "diálogo fallido") para poder probarlos por separado.

## 10. Manejo de errores y casos borde

- **Desconexión de un jugador a media ronda:** su puesto queda vacío (mismo efecto que salirse); el botín que cargaba cae al piso.
- **Desconexión del host:** la ronda termina para todos, regresan al menú con aviso. Sin migración de host en el prototipo. Solo se pierde la ronda en curso.
- **Unirse tarde:** solo en lobby o guarida.
- **Lag:** interpolación de la gabardina; su torpeza de diseño disimula el retraso.
- **Guardado:** Steam Cloud con respaldo local; si ambos fallan se avisa y no se sobrescribe el progreso.
- **Mínimo 2 jugadores.** Modo debug para desarrollo: una persona controla todos los puestos y cambia de rol con una tecla.

## 11. Alcance del prototipo

**Entra:**
- 1 nivel (boda en el jardín).
- De 2 a 5 jugadores en línea vía lobbies de Steam e invitación a amigos.
- Gabardina con puestos escalables y cámara compartida.
- Sospecha, invitados, mesero, gato y diálogos.
- 3 objetivos especiales y botín suelto.
- Salida limpia con multiplicador, "¡RUN!", pánico, 4 salidas y puntaje.
- Guarida con ~8 cosméticos y 2 utensilios.

**Fuera (posterior):**
- Más niveles.
- Modo versus entre gabardinas.
- Matchmaking con desconocidos.
- Chat de voz por proximidad (alta prioridad tras el prototipo, por su valor para clips).
- Más utensilios.
- Página de Steam completa y logros.

## 12. Etapas de desarrollo

0. **Preparación:** Unity Hub + Unity 6, plugin de Claude, git + LFS, reparar el MCP de Blender.
1. **Gabardina gris (local, modo debug):** cuerpo, puestos, salirse/volver, mapache suelto. *Pregunta clave: ¿controlar al señor ya da risa?*
2. **Online:** 2–5 jugadores por Steam, simulación en el host.
3. **Boda gris:** NPCs, gato, mesero, sospecha, diálogos, botín y objetivos.
4. **¡RUN!:** pánico, salidas, puntaje y salida limpia.
5. **Guarida:** progresión, cosméticos, utensilios y guardado.
6. **Pase de arte:** estilo "Pixar ligero" (entre *Fall Guys* e *It Takes Two*): proporciones caricaturescas, iluminación cálida de atardecer, pelaje y telas estilizados. Más animación, efectos y sonido.
7. **Playtest y Steam:** pruebas con amigos, página de Steam y playtest o demo.

## 13. Pruebas

- **Automáticas (Unity Test Framework):** lógica sin visual, como las reglas de sospecha, el reparto de puestos, el cálculo de puntaje y multiplicadores, y el cobro y pérdida de botín.
- **Multijugador local:** Multiplayer Play Mode de Unity.
- **Playtests con amigos:** al final de las etapas 2, 4 y 7.
- **La prueba del clip:** cada playtest se graba; si en 10 minutos no surge un momento que den ganas de compartir, se ajusta antes de avanzar.

## 14. Entorno y restricciones

- Mac con Apple M4, 16 GB de RAM y ~70 GB libres. Suficiente; conviene cerrar aplicaciones pesadas al usar Unity y Blender al mismo tiempo.
- Unity aún no está instalado. Blender sí lo está; su MCP no conecta y hay que repararlo antes de la etapa 6.
