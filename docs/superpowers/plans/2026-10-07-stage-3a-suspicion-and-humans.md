# Etapa 3a: sospecha y humanos — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** La boda gris tiene humanos que **ven** lo raro. Invitados sentados y de pie, un mesero que hace rondas y un gato que patrulla y huele. Un medidor de sospecha compartido sube cuando alguien ve al señor descompuesto o a un mapache suelto, o cuando el gato se acerca, y baja con calma. Al llegar a 100 aparece "¡¡RUUUN!!" (el pánico real es la etapa 4). Funciona igual en línea (lo simula el host) y en modo debug.

**Architecture:** Las reglas viven en `TrashPandas.Core` como lógica pura y probada: `SuspicionModel` (medidor), `VisionCone` (¿lo ve?), `Weirdness` (qué tan raro se ve el señor), `GuestMind` (estados del humano) y `CatMind` (estados del gato). En runtime, un `SuspicionDirector` corre solo donde se simula (host u offline, vía `SimulationAuthority`), alimenta a cada NPC con lo que ve y replica el valor de sospecha y el estado de cada NPC (para los íconos "?" y "!"). Los NPCs se mueven con `NavMeshAgent` sobre un NavMesh horneado por el generador de escena.

**Tech Stack:** Unity 6.6, AI Navigation (`NavMeshSurface`, ya instalado), Netcode for GameObjects 2.13.3, NUnit.

**Spec:** `docs/superpowers/specs/2026-10-07-trash-pandas-trenchcoat-design.md` §5 (Sospecha), §6 (Nivel: zonas, invitados, mesero, gato). Los eventos sociales (§6b) y los diálogos (§7) son la etapa 3b.

## Global Constraints

- Sospecha de 0 a 100, compartida por todo el equipo. Al llegar a 100 se dispara "caught" (por ahora un aviso de "¡¡RUUUN!!"; el pánico es la etapa 4).
- Lo **simula solo el host** (u offline el modo debug). Los clientes reciben el valor y los estados de los NPCs.
- Todos los valores de ajuste quedan expuestos como parámetros (spec §5: "los valores se ajustan en playtest").
- La comedia sale de la descoordinación: lo raro **visto por un humano** es lo que más castiga. Esconderse funciona.
- Los modos debug y online siguen funcionando. Cada tarea se verifica con builds headless y telemetría.

## Decisión de diseño (ruling)

La spec §5 dice que los puestos vacíos suben la sospecha "por segundo". Aquí **suben con tasa completa solo si un humano lo está viendo**, y con un 20% si nadie lo ve. Así esconderse detrás de un seto o de una mesa tiene valor, y salirse cuando nadie mira es una jugada legítima. Queda como parámetro, por si el playtest dice otra cosa.

## Review Focus

1. **Un mapache suelto a la vista de varios invitados a la vez:** la sospecha sube rápido pero no se dispara por N en un solo frame (hay cooldown por testigo).
2. **El gato atorado o en un rincón sin camino:** no se queda vibrando; vuelve a patrullar.
3. **La sospecha llega a 100 en medio de la cuenta:** el aviso sale una sola vez, no cada frame.
4. **Un cliente entra a una ronda con sospecha alta:** ve el valor correcto al instante (NetworkVariable).
5. **Invitado detrás de una mesa o del seto:** no "ve" a través de objetos (raycast de oclusión).

## Tasks

### Task 1: Core: `SuspicionModel` (TDD)
`Core/Suspicion/SuspicionModel.cs` + tests.
- `SuspicionSettings`: `WitnessedMissingPartRate` (por parte faltante, /s, al ser visto), `UnwitnessedFactor` (0.2), `WeirdMovementRate` (/s × rareza vista), `RaccoonSightingBurst` (+20), `SightingCooldown` (2 s por testigo), `CatHissRate` (/s), `CalmDecayRate` (/s), `CalmDelay` (2 s sin eventos antes de bajar), `Max` (100).
- API: `Tick(float dt, in SuspicionFrame frame)`, donde el frame trae las partes faltantes, si alguien ve al señor, la rareza vista (0..1) y si el gato está bufando; `ReportRaccoonSighting(int witnessId, float now) → bool`; `Value`; `Caught` (se activa una vez); `Reset()`.
- Tests: sube con puestos vacíos vistos y menos sin testigos; el avistamiento suma una ráfaga y respeta el cooldown por testigo; varios testigos distintos sí suman; el bufido del gato sube; baja solo después del `CalmDelay`; se limita a [0,100]; `Caught` se activa una sola vez; `Reset` limpia.

### Task 2: Core: `VisionCone` y `Weirdness` (TDD)
`Core/Perception/VisionCone.cs`, `Core/Perception/Weirdness.cs` + tests.
- `VisionCone.CanSee(eye, forward, target, range, fovDegrees, occluded) → bool`.
- `Weirdness.Of(BodyIntent) → 0..1`: colapsado 1.0; cada brazo flojo +0.3; cabeza ladeada +0.3; una pierna floja +0.3; discordia ×0.5; tope en 1.
- Tests: dentro y fuera del rango; dentro y fuera del FOV; ocluido; detrás. Rareza: señor completo y quieto = 0, colapsado = 1, brazo flojo, combinaciones con tope.

### Task 3: Core: `GuestMind` (TDD)
`Core/Npc/GuestMind.cs` + tests.
- Estados: `Calm`, `Curious` (vio rareza ≥ umbral: voltea y se le pone "?"), `Alarmed` (vio un mapache suelto o rareza alta sostenida: "!"; dura unos segundos), y de regreso a `Calm` tras un tiempo sin ver nada.
- API: `Update(float dt, float seenWeirdness, bool seesRaccoon) → GuestState`; `LookTarget` (hacia dónde voltear).
- Tests de cada transición y sus tiempos.

### Task 4: Core: `CatMind` (TDD)
`Core/Npc/CatMind.cs` + tests.
- Estados: `Patrol` (de punto en punto), `Sniffing` (el señor a ≤ 3.5 m: se acerca), `Hissing` (≤ 1.5 m: bufa → sube la sospecha), `Cooldown` (se aleja 4 s después de bufar), y regreso a `Patrol`. Si queda atorado (no avanza en 3 s), salta al siguiente punto de patrulla (Review Focus 2).
- Tests de transiciones, del bufido y del destrabe.

### Task 5: Runtime: autoridad, NPCs y director
- `SimulationAuthority.IsSimulating` = offline o host.
- `NpcPawn` (NavMeshAgent + `NetworkObject` + `NetworkTransform`), con `GuestNpc` (sentado o de pie; voltea la cabeza a `LookTarget`), `WaiterNpc` (ronda carpa → mesas → carpa) y `CatNpc`.
- `SuspicionDirector` (`NetworkBehaviour` en escena): cada frame, si simula, calcula qué ve cada NPC (raycast de los ojos al señor y a cada mapache), hace tick a `GuestMind`/`CatMind`/`SuspicionModel` y replica `NetworkVariable<float> Suspicion`, `NetworkVariable<bool> Caught` y un estado por NPC (`NetworkVariable<byte>` en cada `NpcPawn`).
- Offline (debug) usa los mismos componentes sin red.

### Task 6: Escena y HUD
- El generador agrega: `NavMeshSurface` horneado, unos 10 invitados (sentados en las mesas y 2 grupos de pie), un mesero con su ruta, una carpa del catering (caja) y el gato con 5 puntos de patrulla.
- HUD (debug y online): barra de sospecha arriba, íconos "?" y "!" sobre los NPCs, banner "¡¡RUUUN!!" una sola vez al llegar a 100, y F5 en debug para reiniciar la sospecha.
- `DevAutomation`: telemetría de sospecha y estados de NPCs, y la opción `-shot <seg> <archivo>` para guardar capturas desde una build con ventana.

### Task 7: Verificación
1. **Modo debug (build):** quieto y completo, la sospecha no sube; con bot "hop" frente a invitados, sube y los invitados pasan a "!"; el gato se acerca y bufa.
2. **Online (2 builds):** el cliente ve el mismo valor de sospecha y los mismos íconos que el host.
3. **Capturas** de la boda con NPCs para que el usuario vea cómo se ve.
4. Revisión final de la rama por un revisor independiente.
