# Concepto v2, entrega 1: mapaches — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** La ronda se juega con mapaches sueltos desde el inicio, sin gabardina. Cada jugador tiene controles normales: correr (Shift, ruidoso), escabullirse (C), saltar, y agarrar, soltar o aventar con clic lo que se ilumina, sin apuntar. Lo pesado se carga entre dos o más. Los mapaches se montan uno en otro formando torres. El botín se entrega en la madriguera. El ruido hace que los humanos volteen e investiguen. El RUN, las salidas y los resultados siguen funcionando. En debug se juega solo, con Tab para cambiar de mapache.

**Architecture:** Las reglas puras van en `TrashPandas.Core.Raccoons`: `ClickIntent` (toque contra sostener y aventar), `GrabPick` (qué objeto se ilumina), `HeavyCarry` (cuándo se levanta lo pesado), `TowerRules` (velocidad y caída de la torre), `NoiseModel` (qué tan fuerte suena cada cosa y quién lo oye) y `DeliveryLedger` (botín entregado por jugador y objetivos). En runtime: un `RaccoonCarrier` en cada mapache (boca y carga; host u offline decide, el dueño pide por RPC), un `RaccoonTower` (montarse, del lado del dueño), un `Den` (zona de entrega), un `NoiseBus` que alimenta a los humanos, un `SquadSpawner` (host u offline: un mapache por jugador al empezar) y un `DebugSquad` (offline: varios mapaches, Tab cambia el control). La gabardina queda desactivada en este modo (`GameMode.Raccoons`), sin borrarla.

**Tech Stack:** Unity 6.6, NGO 2.13.3, Input System, AI Navigation, NUnit.

**Spec:** `docs/superpowers/specs/2026-10-07-trash-pandas-v2-design.md` §3, §4, §8, §9 y §10 (entrega 1). Del v1 siguen vigentes la sospecha, el RUN y el mapa (§16).

## Global Constraints

- Teclas: WASD + mouse, Espacio saltar (sostener = más alto), **Shift correr**, **C escabullirse** (nunca Ctrl), **clic** agarrar o soltar (toque < 0.25 s), **sostener clic y soltar** = aventar (fuerza según lo sostenido, máx. 1 s), **E** usar, **Tab** (solo debug) cambiar de mapache.
- Velocidades: normal 3.0 m/s, correr 5.0 m/s, escabullirse 1.5 m/s.
- Agarrar: alcance 1.2 m desde la boca; se ilumina el agarrable más cercano al centro de la vista dentro de un cono de 35°.
- Lo chico se carga en la boca, de uno en uno. Lo pesado (`RequiresBothHands` → "pesado") necesita **2** mapaches; con 1 solo "forcejea" y no se mueve. Si los cargadores se separan más de 1.6 m, se cae.
- Torre: saltar sobre otro mapache te monta; el de abajo va al 60% de velocidad por cada uno que carga, y la torre cae si el de abajo corre o recibe un golpe. Máximo 5.
- Ruido: correr 6 m de radio, aterrizar fuerte 4 m, objeto que cae o se avienta 7 m; escabullirse 0. Los humanos que oyen voltean, se ponen curiosos y caminan a investigar, sin ataque y sin sospecha directa.
- Entrega: el botín cuenta al entrar a la madriguera y queda a salvo; si te atrapan con algo en la boca, lo pierdes. Escapar con algo en la boca lo entrega.
- El host (u offline) decide la carga, las entregas y el ruido; el dueño mueve su mapache. Los textos del juego, en inglés.

## Review Focus

1. **Dos mapaches agarran el mismo objeto chico a la vez:** solo uno lo obtiene.
2. **El de abajo de una torre entra a la madriguera o a una salida:** solo cuenta para él; los de arriba caen y siguen jugando.
3. **Un cargador de algo pesado es atrapado o se desconecta:** el objeto cae, y no queda pegado a nadie.
4. **Montarse sobre un mapache que ya va cargando algo pesado, o que está atrapado:** no se permite.
5. **Online:** el objeto en la boca y la torre se ven pegados en todas las pantallas; nadie empuja con colisiones fantasma.

## Tasks

### Task 1: Core `ClickIntent`, `GrabPick`, `DeliveryLedger` (TDD)
- `ClickIntent`: `Press(now)`, `Release(now) → Tap | Throw(strength 0..1)`.
- `GrabPick.Pick(eye, forward, mouth, candidates, reach=1.2, cone=35°) → id?`: el más centrado dentro del alcance.
- `DeliveryLedger`: `Deliver(player, itemId, value, objective?) → bool` (idempotente), `Of(player)`, `Total`, `IsDone(objective)`, `Reset()`.

### Task 2: Core `HeavyCarry`, `TowerRules`, `NoiseModel` (TDD)
- `HeavyCarry.Lifted(carriers, needed=2)`, `HeavyCarry.Anchor(points) → midpoint`, `HeavyCarry.ShouldDrop(points, maxSpread=1.6)`.
- `TowerRules.SpeedFactor(riders) = 0.6^riders`, `TowerRules.Collapses(bottomRunning, bottomHit)`, `TowerRules.CanMount(target state)`: no si el objetivo carga algo pesado, está congelado o la torre ya tiene 5.
- `NoiseModel.Radius(NoiseKind)` y `NoiseModel.Hears(listener, source, radius, occluded)`.

### Task 3: Runtime: controles del mapache
- `RaccoonController`: correr con Shift y escabullirse con C (velocidades de las Global Constraints), y `SpeedMultiplier` para la torre y la carga.
- `RaccoonInput` (en el lector): Shift, C, clic con `ClickIntent`, E, y el ruido al correr y aterrizar.
- Resaltado: el objeto elegido por `GrabPick` brilla (tinte de emisión) y muestra un "▼" encima.

### Task 4: Runtime: boca, aventar y cargar entre varios
- `RaccoonCarrier`: toque = agarrar el resaltado o soltar lo que traes; aventar = impulso hacia la cámara según la fuerza. Offline directo; online `GrabRpc(itemIndex, ownerPos)` y `ThrowRpc(dir, strength)` al host (que confía en la posición del dueño si está a ≤ 2 m de la suya).
- Lo pesado: cada cargador se registra; con ≥ 2 el host lo sostiene en el punto medio de sus bocas (kinematic) y los cargadores van al 70%; con 1, forcejea; si se separan, cae.
- Todas las pantallas pegan lo cargado a la boca, sin colisión (como en 3c-1).

### Task 5: Runtime: torre de mapaches
- `RaccoonTower` (en el dueño): al caer sobre la cabeza de otro mapache (y si `CanMount`), te montas: tu mapache sigue el ancla de su cabeza y se apaga tu CharacterController; Espacio te bajas.
- El de abajo va más lento (`SpeedFactor`); si corre o recibe un golpe, `Collapse()` tira a todos con un empujón.
- Online: el dueño del de arriba sigue al de abajo tal como lo ve; un `NetworkVariable` de "montado en" para que el host y los demás lo sepan.

### Task 6: Runtime: madriguera, ruido y modo mapaches
- `Den` (zona, en la camioneta del catering): al entrar con algo en la boca, `DeliveryLedger.Deliver`, el objeto desaparece y aparece "+$40".
- `NoiseBus.Emit(pos, kind)`: el host hace que los humanos que oyen volteen y caminen a investigar (`GuestMind` curioso + destino).
- `GameMode.Raccoons`: la gabardina, el perchero y los eventos sociales quedan apagados; `SquadSpawner` crea un mapache por jugador (online, host con ownership; offline, 3 mapaches con `DebugSquad` y Tab); el RUN no revienta nada, solo arma a los humanos; los jugadores del pánico son todos los mapaches; los resultados muestran lo entregado.
- HUD: botín del equipo, objetivos, reloj, lo que traes en la boca, y ayuda de controles nueva.

### Task 7: Verificación
1. Bots offline: `fetch` (va al objeto chico más cercano, lo agarra y lo entrega en la madriguera → +$), `heavy` (dos mapaches del escuadrón cargan el regalo gigante juntos), `tower` (uno se monta en otro y el de abajo camina), `noisy` (corre junto a un invitado → voltea e investiga).
2. Debug con Tab: cambiar de mapache y que los demás se queden quietos.
3. Online (2 builds): los dos aparecen como mapaches, el cliente agarra y entrega, y se ve pegado en el host.
4. Capturas: el resaltado, la torre y la carga pesada.
5. Revisión final independiente.
