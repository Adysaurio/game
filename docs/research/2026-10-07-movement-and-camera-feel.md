# Investigación: fluidez de movimiento, cámara y controles

Fecha: 2026-10-07. Motivo: el primer playtest gris se sintió lento, mareador y con giros erráticos.

## 1. Diagnóstico del playtest

| Síntoma | Causa probable | Evidencia |
|---|---|---|
| La cámara gira "como loca" | Bug conocido de macOS: al bloquear el cursor, `Mouse.delta` devuelve valores grandes y espurios. Nuestro código re-bloqueaba el cursor **cada frame**, así que podía generar picos de delta continuamente | Unity Issue Tracker (abajo). Medición en vivo: sin foco, el cuerpo no gira (yaw estable 326.5°) y la cámara copia exactamente el yaw del cuerpo |
| Giro lento, luego demasiado brusco | El mouse controlaba una *velocidad* de giro del cuerpo, y la cámara estaba amarrada a la espalda del cuerpo | Código de `FollowCamera` y `TrenchcoatBody` |
| "No veo las piernas ni los brazos" | No había mallas de piernas ni brazos | Corregido en el commit `2d23781` |

## 2. Qué hacen los juegos de referencia

**Human: Fall Flat** (el estándar de control "torpe pero preciso"):
- **El mouse controla la cámara**, y **los brazos apuntan hacia donde mira la cámara**. Mirar arriba levanta los brazos; mirar abajo, agarra el suelo.
- Clic izquierdo y derecho = brazo izquierdo y derecho. Mientras lo mantienes, agarra lo que toque.
- Es difícil al inicio, pero es *preciso*: el jugador siempre sabe hacia dónde va su mano.

**Fall Guys** (cómo ser gracioso sin frustrar):
- Su diseñador principal (Joe Walsh): *"si el personaje se cae al azar, el juego deja de ser divertido al instante"*. Las reglas clásicas de plataformas siguen aplicando.
- El personaje se ve torpe (jiggle, rebote), pero **el control es responsivo**. Lo torpe es visual y secundario, no del input.

**Octodad co-op** (la advertencia directa para nosotros):
- La crítica: el co-op *"convierte el caos controlado en caos que los jugadores necesitan desesperadamente controlar"*.
- 👉 **Lección:** cada rol debe sentirse bien y preciso *por sí solo*. La comedia tiene que salir de la **descoordinación entre jugadores**, nunca de controles malos.

**Gang Beasts y otros físicos:** usan ragdoll activo (IK + controladores PID que simulan músculos). Requiere mucho ajuste y es caro de sincronizar en red. **No es para nosotros:** usamos animación procedural más resortes visuales, simulados en el host.

## 3. Técnicas de "game feel" a aplicar

**Movimiento**
- Aceleración y desaceleración (en lugar de velocidad instantánea), con valores distintos en suelo y aire.
- **Coyote time ≈ 0.12 s:** puedes saltar un instante después de salir de un borde.
- **Jump buffer ≈ 0.15 s:** si presionas saltar justo antes de tocar el suelo, el salto sale al aterrizar.
- Gravedad más fuerte al caer que al subir, y salto de altura variable.
- Movimiento **relativo a la cámara** (como en Fall Guys o cualquier juego en tercera persona), con el personaje girando suavemente hacia donde camina.

**Cámara**
- **Cinemachine 3** (paquete oficial de Unity, documentado para Unity 6.6): `CinemachineCamera` + `OrbitalFollow` + `InputAxisController`, con colisión para no atravesar paredes.
- Damping solo en la *posición* de la cámara (valores de referencia X=0.1, Y=0.5, Z=0.3). **Nunca suavizar el input del mouse**, porque agrega lag e imprecisión.
- Rigidbody con `Interpolate`, cámara actualizada en `LateUpdate`; nada de mover la cámara en `FixedUpdate`.

**Mouse**
- Delta crudo, sin suavizado.
- Bloquear el cursor **una sola vez** (no cada frame) e ignorar los primeros frames después del bloqueo (bug de macOS).
- Limitar picos por frame y exponer la sensibilidad en las opciones.

## 4. Propuesta de controles revisada

Cada jugador tiene **su propia cámara orbital** apuntando a la gabardina. Es "compartida" porque todos siguen al mismo señor, pero cada quien la orbita con su mouse.

| Rol | Mouse | Teclado / botones |
|---|---|---|
| **Piernas** | Orbita la cámara | WASD camina **relativo a tu cámara**; el señor gira suave hacia donde camina. Espacio salta (coyote + buffer). Ctrl agacha |
| **Brazos** | Orbita la cámara; **las manos apuntan a donde mira tu cámara** (estilo Human: Fall Flat) | Clic izq./der. = estirar y agarrar con la mano izq./der. |
| **Cabeza** | Orbita la cámara; **la cabeza mira hacia donde mira tu cámara** | Teclas de reacción (sonreír, asentir) y respuestas de diálogo |
| **Mapache suelto** | Orbita la cámara | WASD relativo a la cámara, salto con coyote + buffer, trepar, agacharse |

**Por qué esto genera comedia sin frustrar:** con piernas separadas, cada jugador camina hacia *su* cámara. Si sus cámaras apuntan a lugares distintos, el señor se tuerce, y eso es culpa de la descoordinación, no del control. Cada rol, solo, se siente como un juego en tercera persona normal y preciso.

## Fuentes

- [Designing touch controls for Human: Fall Flat — Game Developer](https://gamedeveloper.com/design/designing-touch-controls-for-human-fall-flat)
- [Human: Fall Flat review — KeenGamer](https://keengamer.com/article/14019_human-fall-flat-review)
- [Developer Q&A: Fall Guys — Digitally Downloaded](https://www.digitallydownloaded.net/?p=42226)
- [Octodad co-op review — Co-Optimus](https://www.co-optimus.com/review/1336/page/2/octodad-dadliest-catch-co-op-review.html)
- [Single Character Co-Op — Giant Bomb](https://giantbomb.com/wiki/Concepts/Single_Character_Co_Op)
- [Physics-based animation for characters — Unity Discussions](https://discussions.unity.com/t/physics-based-animation-system-for-characters/652307)
- [Cinemachine Orbital Follow (6.6) — Unity Docs](https://docs.unity3d.com/Packages/com.unity.cinemachine@6.6/manual/CinemachineOrbitalFollow.html)
- [Cinemachine Third Person Follow — Unity Docs](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineThirdPersonFollow.html)
- [mouseDelta large value when locking cursor (macOS) — Unity Issue Tracker](https://issuetracker.unity.com/issues/12205/mousedelta-returns-a-large-value-when-cursorlockstate-cursorlockmodelocked-is-activated)
- [Mac: mouse delta generated after click while motionless — Unity Issue Tracker](https://issuetracker-mig.prd.it.unity3d.com/issues/mac-mouse-delta-values-are-generated-after-mouse-click-while-the-cursor-is-motionless)
- [Fix Unity Rigidbody jitter with camera follow — Bugnet](https://bugnet.io/blog/fix-unity-rigidbody-jitter-with-camera-follow)
- [Input buffering & coyote time — itch.io devlog](https://tooster.itch.io/chapter-01/devlog/560384/input-buffering-coyote-time)
- [3D character controller defaults (coyote/buffer) — incanto skill](https://cdn.jsdelivr.net/npm/incanto@0.79.1/skills/incanto-3d-character.md)
