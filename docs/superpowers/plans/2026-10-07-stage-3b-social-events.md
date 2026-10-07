# Etapa 3b: eventos sociales — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Durante la infiltración, cada cierto tiempo al azar aparece "¡EVENT IN N!" y un humano (suegra, mesero, cura, novia) camina hacia el señor. Los mapaches sueltos tienen que regresar a la gabardina. Cuando el humano llega, la cámara se acerca y cada rol tiene su parte con tiempo límite: **la cabeza elige qué decir**, **los brazos hacen la acción** (dar la mano, tomar la copa, juntar las manos) y **las piernas** (quedarse quietas, arrodillarse, dar un pasito de baile). Si sale bien, la sospecha baja; si una parte falla o un puesto está vacío, la sospecha sube mucho.

**Architecture:** La lógica pura va en `TrashPandas.Core.Events`: `WarningTime` (aviso dinámico), `EventScheduler` (cuándo y quién), `SocialEventCatalog` (los eventos: líneas, opciones, acciones), `EventTaskTracker` (evalúa lo que hizo cada jugador) y `EventResolver` (cambio de sospecha por parte, incluidos los puestos vacíos). En runtime, un `SocialEventDirector` (`NetworkBehaviour`, simulado en host u offline) dispara los eventos, mueve al NPC, recibe las respuestas de cada jugador (`SubmitEventResponseRpc`) y aplica el resultado con `SuspicionModel.Adjust`. Cada jugador ve su propia parte en el HUD y la cámara se acerca.

**Spec:** `docs/superpowers/specs/2026-10-07-trash-pandas-trenchcoat-design.md` §6b (eventos sociales, idea del usuario) y §7 (diálogos). Simulación aprobada: https://claude.ai/artifact/DRFXx6uoGLE24HPpsi1wq8

## Global Constraints

- Aviso = `clamp(distancia del mapache más lejano ÷ 4 m/s + 3 s, 8 s, 20 s)`. El NPC sale desde lo que camina en ese tiempo.
- Primer evento entre 35 y 50 s de iniciada la ronda; después, cada 45 a 75 s al azar. Nunca el mismo NPC dos veces seguidas. Nunca durante el pánico.
- Ventana de respuesta: 5 s. La cabeza responde con 1/2/3.
- Resultado por parte: buena respuesta −8; respuesta rara +5; absurda o en silencio +25; acción de brazos o piernas fallida +20; **puesto vacío +25 por cada parte que le tocaba**. Todo ajustable.
- En el modo debug, la persona al teclado hace **todas** las partes.
- Los textos del juego van en inglés (convención del proyecto).

## Review Focus

1. **Un jugador regresa a la gabardina en el último instante:** cuenta como presente si estaba sentado al empezar la ventana de respuesta.
2. **Se dispara el RUN durante un evento:** el evento se cancela limpio y el NPC se une al pánico.
3. **Dos jugadores comparten un puesto combinado** (2 jugadores: Arriba = brazos + cabeza): ese jugador hace las dos partes.
4. **Un cliente responde dos veces o tarde:** cuenta la primera respuesta dentro de la ventana.
5. **Nadie hace nada:** el evento resuelve por silencio, sin colgarse.

## Tasks

### Task 1: Core: `WarningTime`, `EventScheduler`, `SuspicionModel.Adjust` (TDD)
### Task 2: Core: `SocialEventCatalog`, `EventTaskTracker`, `EventResolver` (TDD)
- Tareas de brazos: `Handshake` / `TakeGlass` (mantener clic izquierdo ≥ 0.5 s) y `HandsTogether` (clic izquierdo + derecho ≥ 0.5 s).
- Tareas de piernas: `StayStill` (nunca moverse), `Kneel` (Ctrl presionado al final) y `DanceStep` (Espacio durante la ventana).
- Respuestas: `Good`, `Odd`, `Absurd` y silencio.
### Task 3: Runtime: `SocialEventDirector`
Fases `Idle → Warning → Engaged → Resolved`. Elige al NPC, lo hace caminar, toma la foto de los puestos ocupados al empezar `Engaged`, recibe respuestas (RPC u offline), resuelve, ajusta la sospecha y replica un `EventSnapshot`. Se cancela con el RUN.
### Task 4: Runtime: input de tareas, cámara y HUD
Ambos controladores (debug y en línea) leen 1/2/3, clics, Ctrl, Espacio y movimiento durante `Engaged` y los reportan. La cámara se acerca. HUD: banner con la cuenta regresiva y el retrato o nombre del NPC, flecha de dirección, panel del diálogo con temporizador, la parte de cada jugador y un aviso del resultado ("+25: ¡nadie movió los brazos!").
### Task 5: Escena
NPCs nuevos: la suegra, el cura y la novia (también perciben como invitados), puntos de partida alrededor del jardín y el director.
### Task 6: Verificación
Bots: `obey` (responde bien y hace las tareas → la sospecha baja), `ignore` (no hace nada → sube), puesto vacío (un mapache afuera → +25 por parte). Prueba en línea con el cliente como cabeza. Capturas. Revisión independiente.
