# Etapa 1b: rediseño de controles y fluidez — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Controles en tercera persona fluidos y precisos por rol (cámara orbital por jugador, movimiento relativo a la cámara, manos que apuntan hacia donde mira la cámara), corrección del giro loco en macOS y herramientas para probar solo (grabación fantasma).

**Architecture:** Las reglas puras siguen en `TrashPandas.Core`: mezcla por partes con vectores en espacio mundo, `JumpAssist` (coyote/buffer), `MouseLookFilter` (bug de macOS) e `InputGhost` (grabación y reproducción). Runtime usa Cinemachine 6.6 (`CinemachineCamera` + `OrbitalFollow` en modo WorldSpace + `RotationComposer`), manejado por `PlayerCameraRig`, que lee el mouse filtrado. El escenario sigue generándose con `GreyboxSceneBuilder`.

**Tech Stack:** Unity 6.6, Cinemachine 6.6.0, Input System 1.20, Unity Pipeline 0.8 (control del editor en vivo), NUnit.

**Spec:** `docs/superpowers/specs/2026-10-07-trash-pandas-trenchcoat-design.md` §4 (Controles, Cámara). **Research:** `docs/research/2026-10-07-movement-and-camera-feel.md`.

## Global Constraints

- Cada rol debe sentirse preciso por sí solo; la comedia sale de la descoordinación, no del input.
- Mouse crudo, sin suavizado; el cursor se bloquea solo al cambiar de estado; se filtran picos por frame.
- Coyote time 0.12 s, jump buffer 0.15 s.
- Rigidbody interpolado; la cámara se actualiza después de la física; Fixed Timestep 1/60.
- Toda regla nueva en Core lleva test EditMode. Las pruebas corren en el editor abierto con `unity command run_tests` o en batch con `tools/unity-test.sh` (editor cerrado).

## Review Focus

1. **Las dos piernas empujan en direcciones opuestas:** el señor no debe salir disparado ni temblar; avanza poco y marca discordia. → `Mix_OppositeLegs_CancelAndReportFullDiscord`.
2. **Pico de delta del mouse al bloquear el cursor (macOS):** se ignora. → `Filter_IgnoresSettleFramesAfterLock`, `Filter_DropsSpikes`.
3. **Presionar saltar justo antes de aterrizar o justo después de salir de un borde:** salta. → tests de `JumpAssist`.
4. **Una grabación fantasma con salto se repite en bucle:** no salta en cada frame ni nunca. → `Ghost_ReplaysJumpOncePerLoop`.
5. **Dirección de apuntado cero o NaN:** no rompe el cuerpo. → `Mix_SanitizesNaNMoveAndAim`.

## Tasks

### Task 1: Core: entradas por partes en espacio mundo
Files: `Core/Trenchcoat/{SlotInput,PartInputs,BodyIntent,SlotInputRouter,TrenchcoatIntentMixer}.cs`, tests `SlotInputRouterTests`, `TrenchcoatIntentMixerTests`.
- `SlotInput { Vector2 Move (mundo XZ, magnitud ≤1); bool Crouch; float JumpPressedAt; Vector3 Aim (mundo); bool PrimaryReach; bool SecondaryReach; }`
- `BodyIntent { Vector2 Move; float Discord; bool Jump, Crouch, Collapsed, LeftLegLimp, RightLegLimp, LeftArmLimp, RightArmLimp, LeftReach, RightReach, HeadSlumped; Vector3 LeftAim, RightAim, HeadAim; }`
- Reglas: dos piernas → promedio de vectores y `Discord = (1 − cos θ)/2` si ambas se mueven; una pierna → vector × 0.4 rotado 20° hacia el lado faltante; ninguna → colapso. Salto coordinado sin cambios.

### Task 2: Core: `JumpAssist`, `MouseLookFilter`, `InputGhost`
Files: `Core/Movement/JumpAssist.cs`, `Core/Input/MouseLookFilter.cs`, `Core/Debugging/InputGhost.cs`, con sus tests.

### Task 3: Runtime: cámara Cinemachine, lector de input, cuerpo, mapache, controlador
Files: `Runtime/Cameras/PlayerCameraRig.cs` (reemplaza a `FollowCamera.cs`), `Runtime/Input/DebugInputReader.cs`, `Runtime/Trenchcoat/{TrenchcoatBody,TrenchcoatController}.cs`, `Runtime/Raccoon/RaccoonController.cs`, asmdefs (+`Unity.Cinemachine`).

### Task 4: Escena y verificación en vivo
`GreyboxSceneBuilder` crea `CinemachineBrain` + cámara orbital y fija el Fixed Timestep. Verificación con Unity Pipeline: Play, input simulado (W, mouse, clic), medición de yaw y velocidad y capturas de la Game view. Commit.
