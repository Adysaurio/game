# Etapa 3c-1: la boda grande — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** La boda crece a una finca chica (~60 × 50 m) con cinco zonas. Hay botín con valor que los brazos guardan en el bolsillo compartido de la gabardina, 3 objetivos sorteados por ronda, 3 de 5 salidas abiertas al azar (ocultas hasta el RUN), una salida limpia por el arco que paga ×1.5, y una infiltración de 8 min. Los resultados muestran el botín por jugador y los objetivos.

**Architecture:** Las reglas puras van en `TrashPandas.Core.Loot` (`LootPocket`, `StashGesture`, `LootCatalog`) y `TrashPandas.Core.Round` (`RoundSetup`: objetivos, salidas y lugares del botín; `InfiltrationClock`). En runtime, un `LootDirector` (`NetworkBehaviour`, simulado en host u offline) prepara la ronda, detecta lo que se guarda, lleva el bolsillo y los objetivos, y replica un `LootSnapshot`. El `PanicDirector` reparte el bolsillo en el RUN, paga a quien escapa y maneja la salida limpia. Los objetos de botín son los `Grabbable` existentes más un `LootItem` (valor, id de objetivo, guardado). El mapache lleva un objeto en la boca, pedido al host por RPC.

**Tech Stack:** Unity 6.6, NGO 2.13.3, AI Navigation, NUnit.

**Spec:** `docs/superpowers/specs/2026-10-07-trash-pandas-trenchcoat-design.md` §16 (y §3 loop, §6 nivel).

## Global Constraints

- Mapa ≈ 60 × 50 m, con zonas Entrada (S), Jardín (centro), Cocina (O), Casa sin techo (N), Estacionamiento (E), Setos y huerto (orillas).
- Valores: cartera $40, teléfono $60, cubiertos $15, botella $30, comida $5; ~25 objetos sueltos por ronda.
- Guardar = sostener el objeto a ≤ 0.45 m del pecho durante 0.5 s seguidos.
- En el RUN el bolsillo se reparte en partes iguales (el residuo, al primero); cada mapache cobra solo si escapa. El atrapado pierde su parte y suelta lo que lleva en la boca.
- Salida limpia: la gabardina entra al arco **con todos los puestos ocupados** → todos escapan con el bolsillo ×1.5.
- Objetivos: anillo $300, muñequito $250, sobre $200, champán $150, ramo $120, llaves $100. Se sortean 3 distintos, cada uno en uno de sus lugares posibles.
- Salidas: 5 posibles, 3 abiertas, de zonas distintas, todas a ≥ 12 m del centro del jardín; ocultas hasta el RUN.
- Infiltración de 8 min; al vencerse, la sospecha sube +10/s hasta el RUN.
- El host (u offline el debug) decide todo; los clientes reciben snapshots. Los textos del juego, en inglés.

## Review Focus

1. **Un objeto se guarda mientras otra mano o un mapache lo trae:** se guarda una sola vez y nadie se queda "sosteniendo" un objeto guardado.
2. **Atrapan a un mapache con un objetivo en la boca:** el objetivo cae al piso y no cuenta como cumplido.
3. **La gabardina cruza el arco con un puesto vacío:** no es salida limpia (no pasa nada; hay que volver a entrar).
4. **Play again:** el bolsillo, los objetivos, las salidas y los lugares del botín se sortean de nuevo, y nada queda escondido ni sostenido de la ronda anterior.
5. **Las restricciones de salidas no se pueden cumplir** (pocas candidatas): `RoundSetup` regresa lo mejor posible sin colgarse ni repetir.

## Tasks

### Task 1: Core `LootPocket`, `LootCatalog` y `StashGesture` (TDD)
- `LootCatalog`: valores por tipo (`LootKind { Wallet, Phone, Cutlery, Bottle, Food }`) y los 6 objetivos (`ObjectiveId`, nombre, valor).
- `LootPocket`: `Add(int value, ObjectiveId? objective) → bool` (idempotente por id de objeto), `Total`, `ObjectivesDone`, `Split(int[] players) → Dictionary<int,int>`, `CleanExitPayout(players) → ×1.5`, `Reset()`.
- `StashGesture`: `Tick(float distanceToChest, float dt) → bool` (dispara una vez al cumplir 0.5 s a ≤ 0.45 m; se reinicia al alejarse).
- Tests: suma, guardar dos veces no duplica, reparto con residuo, ×1.5, objetivos marcados, gesto que dispara, se reinicia y no dispara dos veces.

### Task 2: Core `RoundSetup` e `InfiltrationClock` (TDD)
- `RoundSetup.PickObjectives(candidates, count=3, Random)`: 3 objetivos distintos, cada uno con un lugar de sus candidatos.
- `RoundSetup.PickExits(exits[] {pos, zone}, center, count=3, minDistance=12, Random)`: de zonas distintas y lejos del centro; si no alcanzan, relaja primero la regla de la zona y luego la distancia, sin repetir.
- `RoundSetup.PickLootSpots(spots, count, Random)`: sin repetir.
- `InfiltrationClock(limit=480, overtimeRate=10)`: `Begin(now)`, `SecondsLeft(now)`, `OvertimeSuspicion(now, dt)`.
- Tests de cada regla, incluido el caso imposible (Review Focus 5) y la repetibilidad con semilla.

### Task 3: Runtime: `LootItem`, `LootDirector` y bolsillo
- `LootItem` (`NetworkBehaviour` en los props de botín): `Kind`, `Value`, `Objective`, `NetworkVariable<bool> Stashed` (escondido = sin render ni collider, estacionado lejos).
- `LootDirector` (host u offline): al empezar la ronda sortea los lugares del botín y los objetivos (activa solo los 3 objetivos) y arranca el reloj. Cada frame, para cada `Grabbable` sostenido por la gabardina, revisa su `StashGesture` contra `TrenchcoatBody.ChestWorld`; al guardar: `ReleaseAll` de esa mano, `Stashed = true`, `Pocket.Add`, y un popup "+$40". Replica un `LootSnapshot` (total, bits de objetivos, segundos restantes, objeto en la boca de cada jugador).
- Infiltración vencida → `SuspicionDirector.AdjustSuspicion(+10/s)`.
- Restart: limpia el bolsillo, regresa todo y vuelve a sortear (Review Focus 4).

### Task 4: Runtime: boca del mapache, pánico y salida limpia
- Mapache: clic con un objeto de botín a ≤ 0.8 m → lo carga en la boca (offline directo; online `RequestMouthGrabRpc` al host, que sigue la posición de la boca del mapache con el objeto kinematic). Clic otra vez lo suelta. Si entra a la gabardina con él, se guarda.
- `PanicDirector`: en el `Burst`, reparte el bolsillo (`Split`). Al escapar con algo en la boca, suma su valor a ese jugador (y el objetivo cuenta). Al quedar atrapado, pierde su parte y suelta lo que lleva (Review Focus 2). `ExitMarkers` y `Exits` salen de las 3 sorteadas.
- Salida limpia: zona del arco; si la gabardina entra en ella con todos los puestos ocupados en infiltración → fase `Results` con todos `Escaped` y el pago ×1.5 (Review Focus 3).
- El `PanicSnapshot` agrega el botín por jugador para los resultados.

### Task 5: Escena: la boda grande
- El generador construye el piso de 64 × 54 m y las zonas: arco de entrada (S), jardín con mesas y pastel (centro), carpa y cocina (O), casa sin techo con pasillo, sala, mesa de regalos y baño (N), estacionamiento con la camioneta y coches (E), y setos y huerto en las orillas.
- Puntos de botín (~40 candidatos, 25 activos), lugares de los objetivos (2–3 por objetivo) y las 5 salidas con su zona.
- Redistribuye a los invitados (~16), al mesero con ruta cocina → jardín → casa, a los oradores y al gato con patrulla por zonas. Navmesh horneado.
- La cámara sigue funcionando en los pasillos (revisión con capturas).

### Task 6: HUD
- Arriba a la izquierda: el bolsillo ($), la lista de objetivos con ✓ y el reloj (rojo en el último minuto).
- Popups "+$40" sobre el pecho al guardar.
- Con el mapache: lo que trae en la boca.
- Resultados: por jugador, escapó o atrapado y $; para el equipo, objetivos ✓/✗, si fue salida limpia y el total.

### Task 7: Verificación
1. Bots nuevos: `stash` (camina a la cartera más cercana, la agarra y la guarda → bolsillo $40) y `walkout` (camina al arco → salida limpia ×1.5).
2. `hopflee` con bolsillo: el reparto y el cobro salen en los resultados.
3. Play again con nuevas semillas: los objetivos y las salidas cambian (telemetría).
4. Online (2 builds): el cliente ve el mismo bolsillo, objetivos y resultados.
5. Capturas de cada zona y de los resultados.
6. Revisión final independiente.
