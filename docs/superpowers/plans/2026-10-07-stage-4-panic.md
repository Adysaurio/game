# Etapa 4: ¡¡RUUUN!! (pánico, armas, salidas, resultados) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Cuando la sospecha llega a 100, la gabardina explota y cada jugador sale como mapache. Los humanos toman el arma más cercana (escoba, sartén, silla, charola) y persiguen y golpean a los mapaches: cada golpe empuja y aturde, y a los 3 golpes quedas atrapado. El gato persigue más rápido y empuja, pero no cuenta golpes. Hay 4 salidas; quien llega a una escapa. Al final aparece una pantalla de resultados con "Jugar otra vez". Antes del RUN los alarmados se acercan a investigar, pero no atacan.

**Architecture:** Las reglas puras van en `TrashPandas.Core.Panic`: `HitTracker` (golpes, aturdimiento, invulnerabilidad breve, atrapado a los 3), `RoundOutcome` (corriendo, escapado o atrapado por jugador, más el fin de ronda por resolución o tiempo), `ChaserMind` (ir por arma → perseguir → golpear con cooldown → cambiar de objetivo), `WeaponAssigner` (cada humano toma el arma libre más cercana) y `ExitZones`. En runtime, un `PanicDirector` (`NetworkBehaviour`) se activa con `SuspicionDirector.Caught`: revienta la gabardina (spawnea un mapache por jugador sentado), pone a los humanos en modo pánico, aplica golpes (al dueño del mapache por RPC, porque su movimiento es del dueño), detecta salidas y replica el resultado. Los resultados se muestran en el HUD y el host puede reiniciar la ronda.

**Spec:** `docs/superpowers/specs/2026-10-07-trash-pandas-trenchcoat-design.md` §3 (loop: RUN y pánico), §6 (salidas), §10. Decisión del usuario (2026-10-07): **los humanos atacan solo después del RUN; antes, si te ven, sube la alerta.**

## Global Constraints

- Ataques **solo en pánico** (después de `Caught`). Antes, los alarmados investigan (caminan hacia donde vieron al mapache) sin golpear.
- 3 golpes = atrapado. Cada golpe empuja (~4 m/s) y aturde 1 s, con 0.8 s de invulnerabilidad para que un mismo swing no cuente doble.
- El gato persigue a 4 m/s y empuja, pero no suma golpes.
- 4 salidas: hueco del seto, alcantarilla, camioneta del catering y fuente.
- La ronda termina cuando todos los jugadores escaparon o fueron atrapados, o a los 90 s de pánico (quien siga corriendo cuenta como atrapado).
- El host decide los golpes, las salidas y el resultado; los clientes reciben los golpes en su propio mapache por RPC.
- Funciona offline (debug) y online. El botín llega con la etapa 3c.

## Review Focus

1. **Un humano golpea a un mapache aturdido o recién golpeado:** no cuenta doble (invulnerabilidad).
2. **Dos humanos van por la misma escoba:** solo uno la toma; el otro busca otra arma o persigue sin ella.
3. **Un jugador se desconecta en pánico:** su resultado queda resuelto (atrapado) y la ronda puede terminar.
4. **Un mapache llega a la salida mientras lo golpean:** escapa una sola vez, y el golpe posterior no lo "atrapa".
5. **El objetivo de un humano escapa o es atrapado:** el humano cambia de objetivo y no se queda corriendo a la nada.

## Tasks

### Task 1: Core `HitTracker` y `RoundOutcome` (TDD)
- `HitTracker(hitsToCatch=3, stun=1, invuln=0.8)`: `TryHit(player, now) → HitResult {Ignored, Stunned, Caught}`, `IsStunned(player, now)`, `Hits(player)`, `Reset()`.
- `RoundOutcome`: `Begin(players, now, limit=90)`, `MarkEscaped(p)`, `MarkCaught(p)` (cada uno idempotente; no se puede pasar de escapado a atrapado ni al revés), `Tick(now)` (al vencerse el tiempo, los que siguen corriendo quedan atrapados), `IsOver`, `StatusOf(p)`, `Escaped`/`Caught` (conteos).

### Task 2: Core `ChaserMind`, `WeaponAssigner`, `ExitZones` (TDD)
- `WeaponAssigner.Assign(humans, weapons) → map`: el arma libre más cercana para cada humano, sin repetir.
- `ChaserMind`: estados `FetchWeapon` → `Chase` → `Swing` (rango 1.2 m, cooldown 1.2 s) y retarget al objetivo vivo más cercano; `Idle` si no quedan objetivos. API: `Update(dt, self, weaponPos?, hasWeapon, targets[]) → (state, destination, swingNow, targetId)`.
- `ExitZones.Contains(exits, pos, radius)`.

### Task 3: Runtime: explosión, humanos en pánico, golpes y salidas
- `PanicDirector` (`NetworkBehaviour`): al ponerse `Caught`, lanza `Burst()` (host u offline): suelta a todos los jugadores sentados, spawnea un mapache por cada uno con impulso hacia afuera y esconde la gabardina. Luego asigna armas, hace tick a cada `ChaserMind`, mueve a los NPCs con su agente (más rápido en pánico) y aplica golpes con `HitTracker` → `RaccoonController.ApplyHit(dir, stun)` (offline directo, online `HitRpc` al dueño). También detecta salidas y lleva `RoundOutcome`.
- `PanicWeapon`: objetos de escena (escobas junto a la carpa, sartenes en la carpa, sillas en las mesas, la charola del mesero) que el humano lleva en la mano.
- `RaccoonController.ApplyHit`: empujón más aturdimiento (ignora input mientras dura).
- Antes del RUN: los invitados de pie y el mesero alarmados caminan hacia la última posición donde vieron al mapache.

### Task 4: Escena, HUD y resultados
- El generador agrega armas, 4 salidas visibles (aros brillantes en el piso con letrero), la camioneta del catering, la fuente y la alcantarilla.
- HUD: corazones o golpes restantes del jugador local, flechas o indicadores hacia las salidas durante el pánico, "¡ESCAPASTE!" / "¡TE ATRAPARON!", y pantalla de resultados con "Jugar otra vez" (host u offline). Los atrapados o escapados ven la acción como espectadores.
- `DevAutomation`: telemetría del pánico (estado por jugador, golpes, armas tomadas) y el bot `flee` (corre a la salida más cercana).

### Task 5: Verificación
1. Debug: provocar el RUN (bot `hop`) → la gabardina explota, los humanos toman armas y persiguen. Con bot inmóvil, el mapache es atrapado tras 3 golpes. Con bot `flee`, escapa y sale la pantalla de resultados.
2. Online (2 builds): los golpes llegan al mapache del cliente y los dos ven el mismo resultado.
3. Capturas del caos.
4. Revisión final independiente.
