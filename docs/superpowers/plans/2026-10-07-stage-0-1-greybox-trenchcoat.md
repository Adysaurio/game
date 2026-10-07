# Etapas 0–1: preparación y gabardina gris — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un proyecto Unity 6 funcionando en el repo, con una escena gris jugable en una sola PC (modo debug). En ella, la gabardina torpe se controla por puestos, un mapache puede salirse y volver, y el mapache suelto corre, salta, trepa y se mete debajo de las mesas.

**Architecture:** Toda la lógica de reglas (puestos, mezcla de controles por parte del cuerpo y posesión en modo debug) vive en una assembly `TrashPandas.Core` de C# puro, probada con EditMode tests. Los `MonoBehaviour` de `TrashPandas.Runtime` solo leen input, llaman a Core y mueven objetos. La escena gris se construye con un script de editor determinista (`GreyboxSceneBuilder`) en lugar de editar YAML de escenas a mano. No hay red en este plan; los ids de jugador ya existen para que la etapa 2 los conecte.

**Tech Stack:** Unity 6 LTS (template Universal 3D / URP), C#, Input System package, Unity Test Framework (NUnit), git + Git LFS, Homebrew.

**Spec:** `docs/superpowers/specs/2026-10-07-trash-pandas-trenchcoat-design.md` (secciones 4, 9, 10, 12 etapas 0–1, 13, 14)

## Global Constraints

- Motor: **Unity 6 LTS**, template **Universal 3D (URP)**. Unity Personal.
- Proyecto Unity en la carpeta `TrashPandas/` dentro del repo; la documentación queda en `docs/`.
- Jugadores por gabardina: **mínimo 2, máximo 5**. El modo debug simula ese mismo número de jugadores con una sola persona.
- Reparto de puestos exacto (spec §4): 2 → Abajo(piernas) · Arriba(brazos+cabeza); 3 → Piernas · Brazos · Cabeza; 4 → Pierna izq · Pierna der · Brazos · Cabeza; 5 → Pierna izq · Pierna der · Brazo izq · Brazo der · Cabeza.
- Puesto vacío ⇒ la función se pierde: sin piernas, el señor se sienta o colapsa; sin un brazo, la manga cuelga; sin cabeza, la cabeza se ladea.
- Controles: piernas con WASD, Espacio para saltar y Ctrl para agacharse; brazos con el mouse para mover las manos y clic para agarrar; `E` para salir y volver; `Tab` para cambiar de puesto en modo debug.
- Infiltración: cámara compartida en tercera persona siguiendo al señor. Mapache suelto: cámara individual en tercera persona.
- Lógica de reglas en `TrashPandas.Core`, sin `MonoBehaviour`. Todo cambio de reglas lleva un test EditMode.
- Código, identificadores, nombres de archivo y commits en inglés; la documentación del plan en español.
- Git LFS para binarios: `*.fbx *.blend *.glb *.png *.psd *.tga *.exr *.wav *.mp3 *.ogg`.
- El modo batch de Unity (tests y scripts) **falla si el editor tiene el proyecto abierto**: hay que cerrar el editor antes de correr `tools/unity-*.sh`.
- Cada commit termina con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Input en diagonal o con valores fuera de rango** (por ejemplo, W+D da magnitud 1.41, o un NaN de un control): la gabardina no debe ir más rápido ni romperse. → Test `Mix_ClampsOutOfRangeAndNaNInputs` en Task 4.
2. **Volver a la gabardina cuando todos los puestos están ocupados:** debe fallar sin duplicar al jugador ni quitarle el puesto a otro. → Test `ReturnToCoat_WhenFull_Fails` en Task 6 y `TryEnter_OccupiedSlot_ReturnsFalse` en Task 3.
3. **Saltar con una sola pierna o con las dos fuera de tiempo:** no debe saltar. La coordinación es el chiste. → Tests `Mix_OneLeg_NeverJumps` y `Mix_LegsJumpOutsideWindow_NoJump` en Task 4.
4. **`Tab` mientras el jugador está afuera como mapache:** no debe abandonar al mapache ni meterlo a un puesto. → Test `CycleNext_WhileOutside_DoesNothing` en Task 6.
5. **Salir dos veces seguidas o entrar estando dentro:** debe ser idempotente y no generar dos mapaches. → Tests `Leave_WhenNotInside_ReturnsFalse` y `TryEnter_PlayerAlreadyInside_ReturnsFalse` en Task 3. Además, `TrenchcoatController` solo crea el mapache si `LeaveCoat()` devolvió `true` (Task 7).

---

## File Structure

```
.gitignore                                   # Unity + macOS ignores
.gitattributes                               # Git LFS patterns
tools/unity-test.sh                          # corre EditMode tests en batch e imprime resumen
tools/unity-run.sh                           # corre un -executeMethod en batch
TrashPandas/                                 # proyecto Unity (lo crea Unity Hub)
  Assets/Scripts/Core/
    TrashPandas.Core.asmdef
    Trenchcoat/BodyPart.cs                   # flags de partes del cuerpo
    Trenchcoat/SlotLayout.cs                 # reparto de puestos por # de jugadores
    Trenchcoat/SlotSystem.cs                 # ocupación de puestos, partes faltantes
    Trenchcoat/PartInputs.cs                 # LegInput, ArmInput, HeadInput, PartInputs
    Trenchcoat/BodyIntent.cs                 # resultado de mezclar controles
    Trenchcoat/TrenchcoatIntentMixer.cs      # reglas: desincronía, cojera, colapso, salto coordinado
    Trenchcoat/SlotInput.cs                  # lo que manda un jugador desde su puesto
    Trenchcoat/SlotInputRouter.cs            # SlotInput por jugador → PartInputs
    Debugging/DebugPossessionModel.cs        # una persona controla todos los puestos
  Assets/Scripts/Runtime/
    TrashPandas.Runtime.asmdef
    Input/DebugInputReader.cs                # teclado/mouse → SlotInput / input de mapache
    Trenchcoat/TrenchcoatBody.cs             # cuerpo gris procedural (Rigidbody)
    Trenchcoat/TrenchcoatController.cs       # orquesta Core + cuerpo + mapache + cámara + HUD
    Raccoon/RaccoonController.cs             # correr, saltar, trepar, agacharse
    Raccoon/Climbable.cs                     # marcador de superficies trepables
    Cameras/FollowCamera.cs                  # cámara de tercera persona
  Assets/Scripts/Editor/
    TrashPandas.Editor.asmdef
    GreyboxSceneBuilder.cs                   # construye Greybox_Trenchcoat.unity y Raccoon.prefab
  Assets/Tests/EditMode/
    TrashPandas.Tests.EditMode.asmdef
    SmokeTests.cs
    SlotLayoutTests.cs
    SlotSystemTests.cs
    TrenchcoatIntentMixerTests.cs
    SlotInputRouterTests.cs
    DebugPossessionModelTests.cs
```

---

### Task 0: Instalar herramientas y crear el proyecto Unity (con el usuario)

Esta tarea requiere al usuario, porque implica instalar con interfaz gráfica e iniciar sesión. El agente guía y verifica.

**Files:**
- Create (via Unity Hub): `TrashPandas/` (proyecto Unity completo)

**Interfaces:**
- Produces: proyecto en `TrashPandas/` con `ProjectSettings/ProjectVersion.txt` (línea `m_EditorVersion: <versión>`), y Unity instalado en `/Applications/Unity/Hub/Editor/<versión>/`.

- [ ] **Step 1: Instalar Git LFS y Unity Hub**

```bash
brew install git-lfs
git lfs install
brew install --cask unity-hub
```
Expected: `git lfs version` imprime una versión; existe `/Applications/Unity Hub.app`.

- [ ] **Step 2 (usuario): Instalar el editor**

En Unity Hub: iniciar sesión → *Installs* → *Install Editor* → la versión **Unity 6 LTS** más reciente (Apple silicon). En módulos, marcar **Windows Build Support (Mono)** (Steam es mayoritariamente Windows). Mac Build Support ya viene incluido.

Verificar:
```bash
ls /Applications/Unity/Hub/Editor/
```
Expected: una carpeta `6000.x.yf1` (o similar).

- [ ] **Step 3 (usuario): Crear el proyecto**

En Unity Hub: *Projects* → *New project* → template **Universal 3D** → *Project name* `TrashPandas` → *Location* `/Users/adyx/Documents/GitHub/game` → desmarcar "Connect to Unity Cloud" → *Create project*. Cuando abra el editor, **cerrarlo**.

Verificar:
```bash
cat TrashPandas/ProjectSettings/ProjectVersion.txt
grep -E '"com.unity.(inputsystem|test-framework|render-pipelines.universal)"' TrashPandas/Packages/manifest.json
```
Expected: se ve la versión; aparecen las tres dependencias. Si falta `com.unity.inputsystem`, abrir el editor → *Window > Package Manager* → *Unity Registry* → **Input System** → *Install*, aceptar el reinicio, cerrar el editor y repetir la verificación.

- [ ] **Step 4 (usuario, recomendado): Plugin de Unity para Claude Code**

En Claude Code ejecutar `/plugin`, buscar **Unity** e instalar el plugin oficial de Unity. Seguir su configuración (instala el Unity CLI con su servidor MCP). Reiniciar Claude Code.
Expected: al listar herramientas aparecen las de Unity. *Este plan no depende de ellas* (usa scripts batch), pero las siguientes etapas sí las aprovechan.

- [ ] **Step 5 (usuario): Reparar el MCP de Blender**

Abrir Blender → en el viewport presionar `N` → pestaña **BlenderMCP** → *Connect / Start server*. Luego en Claude Code ejecutar `/mcp` y reconectar `blender`.
Expected: `blender` aparece como conectado en `/mcp`. Si no hay pestaña BlenderMCP, el addon no está instalado o habilitado: *Edit > Preferences > Add-ons*, buscar "MCP" y habilitarlo. Si sigue sin conectar, anotarlo y seguir: no bloquea este plan.

---

### Task 1: Higiene del repo, scripts de prueba y assemblies

**Files:**
- Create: `.gitignore`, `.gitattributes`, `tools/unity-test.sh`, `tools/unity-run.sh`
- Create: `TrashPandas/Assets/Scripts/Core/TrashPandas.Core.asmdef`
- Create: `TrashPandas/Assets/Scripts/Runtime/TrashPandas.Runtime.asmdef`
- Create: `TrashPandas/Assets/Scripts/Editor/TrashPandas.Editor.asmdef`
- Create: `TrashPandas/Assets/Tests/EditMode/TrashPandas.Tests.EditMode.asmdef`
- Test: `TrashPandas/Assets/Tests/EditMode/SmokeTests.cs`

**Interfaces:**
- Produces: `tools/unity-test.sh [filtro]` (exit 0 si todo pasa; imprime `total=".." passed=".." failed=".."`), `tools/unity-run.sh <Namespace.Class.Method>`, assemblies `TrashPandas.Core`, `TrashPandas.Runtime`, `TrashPandas.Editor`, `TrashPandas.Tests.EditMode`.

- [ ] **Step 1: Escribir `.gitignore`**

```gitignore
# macOS
.DS_Store

# Unity
/TrashPandas/[Ll]ibrary/
/TrashPandas/[Tt]emp/
/TrashPandas/[Oo]bj/
/TrashPandas/[Bb]uild/
/TrashPandas/[Bb]uilds/
/TrashPandas/[Ll]ogs/
/TrashPandas/[Uu]ser[Ss]ettings/
/TrashPandas/[Mm]emoryCaptures/
/TrashPandas/.vs/
/TrashPandas/.idea/
/TrashPandas/*.csproj
/TrashPandas/*.sln
/TrashPandas/*.slnx

# Local test output
/.test-results/
```

- [ ] **Step 2: Escribir `.gitattributes`**

```gitattributes
*.fbx  filter=lfs diff=lfs merge=lfs -text
*.blend filter=lfs diff=lfs merge=lfs -text
*.glb  filter=lfs diff=lfs merge=lfs -text
*.png  filter=lfs diff=lfs merge=lfs -text
*.psd  filter=lfs diff=lfs merge=lfs -text
*.tga  filter=lfs diff=lfs merge=lfs -text
*.exr  filter=lfs diff=lfs merge=lfs -text
*.wav  filter=lfs diff=lfs merge=lfs -text
*.mp3  filter=lfs diff=lfs merge=lfs -text
*.ogg  filter=lfs diff=lfs merge=lfs -text
```

- [ ] **Step 3: Escribir `tools/unity-test.sh`** (compatible con bash 3.2 de macOS)

```bash
#!/usr/bin/env bash
# Runs Unity EditMode tests in batch mode. Close the Unity editor first.
# Usage: tools/unity-test.sh [testFilter]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/TrashPandas"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt")"
UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
OUT="$ROOT/.test-results"
RESULTS="$OUT/editmode.xml"
LOG="$OUT/editmode.log"
mkdir -p "$OUT"
rm -f "$RESULTS"

ARGS=(-batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode -testResults "$RESULTS" -logFile "$LOG")
if [ $# -gt 0 ]; then ARGS+=(-testFilter "$1"); fi

set +e
"$UNITY" "${ARGS[@]}"
CODE=$?
set -e

if [ ! -f "$RESULTS" ]; then
  echo "No test results produced (compile error or editor open?). Last log lines:"
  tail -n 40 "$LOG"
  exit 1
fi
grep -o '<test-run [^>]*>' "$RESULTS" | grep -oE '(total|passed|failed)="[0-9]+"' | tr '\n' ' '
echo
if [ "$CODE" -ne 0 ]; then
  grep -B1 -A6 'result="Failed"' "$RESULTS" | grep -E 'fullname=|<message>' | sed 's/^ *//' || true
fi
exit "$CODE"
```

- [ ] **Step 4: Escribir `tools/unity-run.sh`**

```bash
#!/usr/bin/env bash
# Runs a static editor method in batch mode. Close the Unity editor first.
# Usage: tools/unity-run.sh TrashPandas.EditorTools.GreyboxSceneBuilder.Build
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/TrashPandas"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt")"
UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
mkdir -p "$ROOT/.test-results"
LOG="$ROOT/.test-results/run.log"
set +e
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -executeMethod "$1" -logFile "$LOG"
CODE=$?
set -e
if [ "$CODE" -ne 0 ]; then tail -n 40 "$LOG"; fi
exit "$CODE"
```

Run: `chmod +x tools/unity-test.sh tools/unity-run.sh`

- [ ] **Step 5: Escribir las asmdef**

`TrashPandas/Assets/Scripts/Core/TrashPandas.Core.asmdef`:
```json
{
  "name": "TrashPandas.Core",
  "rootNamespace": "TrashPandas.Core",
  "references": [],
  "autoReferenced": true
}
```

`TrashPandas/Assets/Scripts/Runtime/TrashPandas.Runtime.asmdef`:
```json
{
  "name": "TrashPandas.Runtime",
  "rootNamespace": "TrashPandas.Runtime",
  "references": ["TrashPandas.Core", "Unity.InputSystem"],
  "autoReferenced": true
}
```

`TrashPandas/Assets/Scripts/Editor/TrashPandas.Editor.asmdef`:
```json
{
  "name": "TrashPandas.Editor",
  "rootNamespace": "TrashPandas.EditorTools",
  "references": ["TrashPandas.Core", "TrashPandas.Runtime"],
  "includePlatforms": ["Editor"],
  "autoReferenced": false
}
```

`TrashPandas/Assets/Tests/EditMode/TrashPandas.Tests.EditMode.asmdef`:
```json
{
  "name": "TrashPandas.Tests.EditMode",
  "rootNamespace": "TrashPandas.Tests",
  "references": ["TrashPandas.Core", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

- [ ] **Step 6: Escribir el smoke test**

`TrashPandas/Assets/Tests/EditMode/SmokeTests.cs`:
```csharp
using NUnit.Framework;

namespace TrashPandas.Tests
{
    public class SmokeTests
    {
        [Test]
        public void TestPipeline_Runs()
        {
            Assert.AreEqual(4, 2 + 2);
        }
    }
}
```

- [ ] **Step 7: Correr las pruebas** (editor cerrado)

Run: `tools/unity-test.sh`
Expected: `total="1" passed="1" failed="0"`, exit 0. La primera corrida tarda varios minutos porque importa el proyecto.

- [ ] **Step 8: Commit**

```bash
git add .gitignore .gitattributes tools TrashPandas
git status --short | head -20   # verificar que Library/ y Temp/ NO aparecen
git commit -m "chore: add Unity project, test scripts and assembly definitions

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Partes del cuerpo y reparto de puestos

**Files:**
- Create: `TrashPandas/Assets/Scripts/Core/Trenchcoat/BodyPart.cs`
- Create: `TrashPandas/Assets/Scripts/Core/Trenchcoat/SlotLayout.cs`
- Test: `TrashPandas/Assets/Tests/EditMode/SlotLayoutTests.cs`

**Interfaces:**
- Produces: `[Flags] enum BodyPart { None, LegLeft, LegRight, ArmLeft, ArmRight, Head, Legs, Arms, All }`; `static class SlotLayout { const int MinPlayers = 2; const int MaxPlayers = 5; static IReadOnlyList<BodyPart> ForPlayerCount(int playerCount) }` (lanza `ArgumentOutOfRangeException` fuera de 2–5).

- [ ] **Step 1: Escribir el test que falla**

`TrashPandas/Assets/Tests/EditMode/SlotLayoutTests.cs`:
```csharp
using System;
using System.Linq;
using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Tests
{
    public class SlotLayoutTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Layout_HasOneSlotPerPlayer_CoversWholeBody_WithoutOverlap(int players)
        {
            var slots = SlotLayout.ForPlayerCount(players);

            Assert.AreEqual(players, slots.Count);
            var union = slots.Aggregate(BodyPart.None, (acc, p) => acc | p);
            Assert.AreEqual(BodyPart.All, union);
            for (int i = 0; i < slots.Count; i++)
                for (int j = i + 1; j < slots.Count; j++)
                    Assert.AreEqual(BodyPart.None, slots[i] & slots[j], $"slots {i} and {j} overlap");
        }

        [Test]
        public void Layout_MatchesSpec()
        {
            CollectionAssert.AreEqual(
                new[] { BodyPart.Legs, BodyPart.Arms | BodyPart.Head },
                SlotLayout.ForPlayerCount(2));
            CollectionAssert.AreEqual(
                new[] { BodyPart.Legs, BodyPart.Arms, BodyPart.Head },
                SlotLayout.ForPlayerCount(3));
            CollectionAssert.AreEqual(
                new[] { BodyPart.LegLeft, BodyPart.LegRight, BodyPart.Arms, BodyPart.Head },
                SlotLayout.ForPlayerCount(4));
            CollectionAssert.AreEqual(
                new[] { BodyPart.LegLeft, BodyPart.LegRight, BodyPart.ArmLeft, BodyPart.ArmRight, BodyPart.Head },
                SlotLayout.ForPlayerCount(5));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(6)]
        public void Layout_RejectsUnsupportedPlayerCounts(int players)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SlotLayout.ForPlayerCount(players));
        }
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `tools/unity-test.sh SlotLayoutTests`
Expected: exit 1 con "No test results produced" y en el log `error CS0246: The type or namespace name 'BodyPart' could not be found`.

- [ ] **Step 3: Implementar**

`TrashPandas/Assets/Scripts/Core/Trenchcoat/BodyPart.cs`:
```csharp
using System;

namespace TrashPandas.Core.Trenchcoat
{
    [Flags]
    public enum BodyPart
    {
        None = 0,
        LegLeft = 1 << 0,
        LegRight = 1 << 1,
        ArmLeft = 1 << 2,
        ArmRight = 1 << 3,
        Head = 1 << 4,
        Legs = LegLeft | LegRight,
        Arms = ArmLeft | ArmRight,
        All = Legs | Arms | Head,
    }
}
```

`TrashPandas/Assets/Scripts/Core/Trenchcoat/SlotLayout.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>Which body parts each slot controls, by player count (spec §4).</summary>
    public static class SlotLayout
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 5;

        static readonly BodyPart[][] Layouts =
        {
            new[] { BodyPart.Legs, BodyPart.Arms | BodyPart.Head },
            new[] { BodyPart.Legs, BodyPart.Arms, BodyPart.Head },
            new[] { BodyPart.LegLeft, BodyPart.LegRight, BodyPart.Arms, BodyPart.Head },
            new[] { BodyPart.LegLeft, BodyPart.LegRight, BodyPart.ArmLeft, BodyPart.ArmRight, BodyPart.Head },
        };

        public static IReadOnlyList<BodyPart> ForPlayerCount(int playerCount)
        {
            if (playerCount < MinPlayers || playerCount > MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount,
                    $"A trenchcoat supports {MinPlayers}-{MaxPlayers} players.");
            return Array.AsReadOnly(Layouts[playerCount - MinPlayers]);
        }
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `tools/unity-test.sh SlotLayoutTests`
Expected: `total="8" passed="8" failed="0"`

- [ ] **Step 5: Commit**

```bash
git add TrashPandas/Assets/Scripts/Core TrashPandas/Assets/Tests/EditMode
git commit -m "feat(core): add body parts and slot layout per player count

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Ocupación de puestos (`SlotSystem`)

**Files:**
- Create: `TrashPandas/Assets/Scripts/Core/Trenchcoat/SlotSystem.cs`
- Test: `TrashPandas/Assets/Tests/EditMode/SlotSystemTests.cs`

**Interfaces:**
- Consumes: `BodyPart`, `SlotLayout.ForPlayerCount(int)`.
- Produces:
  ```csharp
  public sealed class SlotSystem {
      public SlotSystem(int playerCount);
      public int SlotCount { get; }
      public BodyPart PartsOf(int slotIndex);
      public int? OccupantOf(int slotIndex);
      public int? SlotOf(int playerId);
      public int? FirstFreeSlot();
      public bool TryEnter(int playerId, int slotIndex);
      public bool Leave(int playerId);
      public BodyPart ControlledParts { get; }
      public BodyPart MissingParts { get; }
      public event Action<BodyPart> MissingPartsChanged; // argumento: nuevas partes faltantes
  }
  ```

- [ ] **Step 1: Escribir el test que falla**

`TrashPandas/Assets/Tests/EditMode/SlotSystemTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Tests
{
    public class SlotSystemTests
    {
        [Test]
        public void NewSystem_AllSlotsFree_EverythingMissing()
        {
            var slots = new SlotSystem(3);
            Assert.AreEqual(3, slots.SlotCount);
            Assert.AreEqual(0, slots.FirstFreeSlot());
            Assert.AreEqual(BodyPart.None, slots.ControlledParts);
            Assert.AreEqual(BodyPart.All, slots.MissingParts);
        }

        [Test]
        public void TryEnter_FreeSlot_TakesItsParts()
        {
            var slots = new SlotSystem(3);
            Assert.IsTrue(slots.TryEnter(playerId: 7, slotIndex: 1));
            Assert.AreEqual(7, slots.OccupantOf(1));
            Assert.AreEqual(1, slots.SlotOf(7));
            Assert.AreEqual(BodyPart.Arms, slots.ControlledParts);
            Assert.AreEqual(BodyPart.Legs | BodyPart.Head, slots.MissingParts);
        }

        [Test]
        public void TryEnter_OccupiedSlot_ReturnsFalse()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0);
            Assert.IsFalse(slots.TryEnter(2, 0));
            Assert.AreEqual(1, slots.OccupantOf(0));
            Assert.IsNull(slots.SlotOf(2));
        }

        [Test]
        public void TryEnter_PlayerAlreadyInside_ReturnsFalse()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0);
            Assert.IsFalse(slots.TryEnter(1, 2));
            Assert.AreEqual(0, slots.SlotOf(1));
            Assert.IsNull(slots.OccupantOf(2));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void TryEnter_InvalidSlot_ReturnsFalse(int slotIndex)
        {
            var slots = new SlotSystem(3);
            Assert.IsFalse(slots.TryEnter(1, slotIndex));
        }

        [Test]
        public void Leave_FreesSlot_AndPartGoesMissing()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0);
            slots.TryEnter(2, 1);
            slots.TryEnter(3, 2);
            Assert.AreEqual(BodyPart.None, slots.MissingParts);

            Assert.IsTrue(slots.Leave(2));
            Assert.IsNull(slots.OccupantOf(1));
            Assert.AreEqual(BodyPart.Arms, slots.MissingParts);
            Assert.AreEqual(1, slots.FirstFreeSlot());
        }

        [Test]
        public void Leave_WhenNotInside_ReturnsFalse()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0);
            Assert.IsTrue(slots.Leave(1));
            Assert.IsFalse(slots.Leave(1));
            Assert.IsFalse(slots.Leave(99));
        }

        [Test]
        public void FirstFreeSlot_WhenFull_IsNull()
        {
            var slots = new SlotSystem(2);
            slots.TryEnter(1, 0);
            slots.TryEnter(2, 1);
            Assert.IsNull(slots.FirstFreeSlot());
        }

        [Test]
        public void MissingPartsChanged_FiresOnlyOnRealChanges()
        {
            var slots = new SlotSystem(3);
            var events = new List<BodyPart>();
            slots.MissingPartsChanged += events.Add;

            slots.TryEnter(1, 0);   // legs arrive
            slots.TryEnter(1, 1);   // rejected: no event
            slots.Leave(2);         // not inside: no event
            slots.Leave(1);         // legs leave

            CollectionAssert.AreEqual(
                new[] { BodyPart.Arms | BodyPart.Head, BodyPart.All },
                events);
        }
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `tools/unity-test.sh SlotSystemTests`
Expected: exit 1, log con `error CS0246: The type or namespace name 'SlotSystem' could not be found`.

- [ ] **Step 3: Implementar**

`TrashPandas/Assets/Scripts/Core/Trenchcoat/SlotSystem.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>Tracks which player occupies each trenchcoat slot and which body parts are missing.</summary>
    public sealed class SlotSystem
    {
        readonly IReadOnlyList<BodyPart> _slots;
        readonly int?[] _occupants;

        public event Action<BodyPart> MissingPartsChanged;

        public SlotSystem(int playerCount)
        {
            _slots = SlotLayout.ForPlayerCount(playerCount);
            _occupants = new int?[_slots.Count];
        }

        public int SlotCount => _slots.Count;

        public BodyPart PartsOf(int slotIndex) => _slots[slotIndex];

        public int? OccupantOf(int slotIndex) => _occupants[slotIndex];

        public int? SlotOf(int playerId)
        {
            for (int i = 0; i < _occupants.Length; i++)
                if (_occupants[i] == playerId) return i;
            return null;
        }

        public int? FirstFreeSlot()
        {
            for (int i = 0; i < _occupants.Length; i++)
                if (!_occupants[i].HasValue) return i;
            return null;
        }

        public bool TryEnter(int playerId, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _occupants.Length) return false;
            if (_occupants[slotIndex].HasValue || SlotOf(playerId).HasValue) return false;

            var before = MissingParts;
            _occupants[slotIndex] = playerId;
            NotifyIfChanged(before);
            return true;
        }

        public bool Leave(int playerId)
        {
            var slot = SlotOf(playerId);
            if (!slot.HasValue) return false;

            var before = MissingParts;
            _occupants[slot.Value] = null;
            NotifyIfChanged(before);
            return true;
        }

        public BodyPart ControlledParts
        {
            get
            {
                var parts = BodyPart.None;
                for (int i = 0; i < _occupants.Length; i++)
                    if (_occupants[i].HasValue) parts |= _slots[i];
                return parts;
            }
        }

        public BodyPart MissingParts => BodyPart.All & ~ControlledParts;

        void NotifyIfChanged(BodyPart before)
        {
            var after = MissingParts;
            if (after != before) MissingPartsChanged?.Invoke(after);
        }
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `tools/unity-test.sh SlotSystemTests`
Expected: `total="10" passed="10" failed="0"`

- [ ] **Step 5: Commit**

```bash
git add TrashPandas/Assets/Scripts/Core TrashPandas/Assets/Tests/EditMode
git commit -m "feat(core): add slot occupancy with missing-part tracking

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Mezclador de controles (`TrenchcoatIntentMixer`)

Aquí viven las reglas que hacen gracioso al señor: si las piernas van desincronizadas, gira; con una pierna, cojea; sin piernas, colapsa; el salto necesita que las dos piernas se coordinen.

**Files:**
- Create: `TrashPandas/Assets/Scripts/Core/Trenchcoat/PartInputs.cs`
- Create: `TrashPandas/Assets/Scripts/Core/Trenchcoat/BodyIntent.cs`
- Create: `TrashPandas/Assets/Scripts/Core/Trenchcoat/TrenchcoatIntentMixer.cs`
- Test: `TrashPandas/Assets/Tests/EditMode/TrenchcoatIntentMixerTests.cs`

**Interfaces:**
- Consumes: `BodyPart`.
- Produces:
  ```csharp
  public struct LegInput  { public float Drive; public float Steer; public float JumpPressedAt; public bool Crouch; public static LegInput Idle { get; } }
  public struct ArmInput  { public Vector3 HandTarget; public bool Grab; }
  public struct HeadInput { public float Yaw; public float Pitch; }
  public struct PartInputs { public LegInput LegLeft, LegRight; public ArmInput ArmLeft, ArmRight; public HeadInput Head; public static PartInputs Idle { get; } }
  public struct BodyIntent { public float Forward, Turn; public bool Jump, Crouch, Collapsed;
      public bool LeftArmLimp, RightArmLimp; public Vector3 LeftHandTarget, RightHandTarget; public bool LeftGrab, RightGrab;
      public bool HeadSlumped; public float HeadYaw, HeadPitch; }
  public sealed class MixerSettings { public float JumpWindow = 0.25f; public float DesyncTurnFactor = 1f; public float LimpSpeedFactor = 0.4f; public float LimpTurnBias = 0.3f; }
  public static class TrenchcoatIntentMixer { public static BodyIntent Mix(in PartInputs input, BodyPart present, float now, MixerSettings settings); }
  ```
  Convenciones: `Turn > 0` = girar a la derecha. `JumpPressedAt` = tiempo (s) del último press; `float.NegativeInfinity` = nunca. `HandTarget` está en espacio local del cuerpo.

- [ ] **Step 1: Escribir el test que falla**

`TrashPandas/Assets/Tests/EditMode/TrenchcoatIntentMixerTests.cs`:
```csharp
using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class TrenchcoatIntentMixerTests
    {
        static readonly MixerSettings Settings = new MixerSettings();
        const float Eps = 1e-4f;

        static PartInputs Legs(float driveL, float driveR, float steer = 0f)
        {
            var input = PartInputs.Idle;
            input.LegLeft.Drive = driveL;
            input.LegRight.Drive = driveR;
            input.LegLeft.Steer = steer;
            input.LegRight.Steer = steer;
            return input;
        }

        [Test]
        public void Mix_SyncedLegs_WalkStraight()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(1f, 1f), BodyPart.All, 0f, Settings);
            Assert.AreEqual(1f, intent.Forward, Eps);
            Assert.AreEqual(0f, intent.Turn, Eps);
            Assert.IsFalse(intent.Collapsed);
        }

        [Test]
        public void Mix_LeftLegFaster_TurnsRight()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(1f, 0f), BodyPart.All, 0f, Settings);
            Assert.AreEqual(0.5f, intent.Forward, Eps);
            Assert.Greater(intent.Turn, 0f);
        }

        [Test]
        public void Mix_ClampsOutOfRangeAndNaNInputs()
        {
            var input = Legs(1.41f, 1.41f, steer: 5f);
            var intent = TrenchcoatIntentMixer.Mix(input, BodyPart.All, 0f, Settings);
            Assert.AreEqual(1f, intent.Forward, Eps);
            Assert.AreEqual(1f, intent.Turn, Eps);

            var nan = Legs(float.NaN, float.NaN, steer: float.NaN);
            var nanIntent = TrenchcoatIntentMixer.Mix(nan, BodyPart.All, 0f, Settings);
            Assert.AreEqual(0f, nanIntent.Forward, Eps);
            Assert.AreEqual(0f, nanIntent.Turn, Eps);
        }

        [Test]
        public void Mix_NoLegs_CollapsesAndCannotMove()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(1f, 1f), BodyPart.Arms | BodyPart.Head, 0f, Settings);
            Assert.IsTrue(intent.Collapsed);
            Assert.AreEqual(0f, intent.Forward, Eps);
            Assert.AreEqual(0f, intent.Turn, Eps);
            Assert.IsFalse(intent.Jump);
        }

        [Test]
        public void Mix_OneLeg_LimpsSlowerAndDriftsTowardMissingSide()
        {
            var onlyLeft = BodyPart.All & ~BodyPart.LegRight;
            var intent = TrenchcoatIntentMixer.Mix(Legs(1f, 0f), onlyLeft, 0f, Settings);
            Assert.AreEqual(Settings.LimpSpeedFactor, intent.Forward, Eps);
            Assert.Greater(intent.Turn, 0f, "missing right leg drags the body to the right");

            var onlyRight = BodyPart.All & ~BodyPart.LegLeft;
            var intent2 = TrenchcoatIntentMixer.Mix(Legs(0f, 1f), onlyRight, 0f, Settings);
            Assert.Less(intent2.Turn, 0f, "missing left leg drags the body to the left");
        }

        [Test]
        public void Mix_LegsJumpWithinWindow_Jumps()
        {
            var input = PartInputs.Idle;
            input.LegLeft.JumpPressedAt = 10.00f;
            input.LegRight.JumpPressedAt = 10.20f;
            var intent = TrenchcoatIntentMixer.Mix(input, BodyPart.All, now: 10.21f, Settings);
            Assert.IsTrue(intent.Jump);
        }

        [Test]
        public void Mix_LegsJumpOutsideWindow_NoJump()
        {
            var input = PartInputs.Idle;
            input.LegLeft.JumpPressedAt = 10.00f;
            input.LegRight.JumpPressedAt = 10.30f;
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(input, BodyPart.All, now: 10.31f, Settings).Jump,
                "presses too far apart");

            input.LegLeft.JumpPressedAt = 10.00f;
            input.LegRight.JumpPressedAt = 10.00f;
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(input, BodyPart.All, now: 11.00f, Settings).Jump,
                "presses are stale");
        }

        [Test]
        public void Mix_OneLeg_NeverJumps()
        {
            var input = PartInputs.Idle;
            input.LegLeft.JumpPressedAt = 5f;
            input.LegRight.JumpPressedAt = 5f;
            var onlyLeft = BodyPart.All & ~BodyPart.LegRight;
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(input, onlyLeft, now: 5f, Settings).Jump);
        }

        [Test]
        public void Mix_IdleInputs_NeverJump()
        {
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(PartInputs.Idle, BodyPart.All, now: 0f, Settings).Jump);
        }

        [Test]
        public void Mix_MissingArm_IsLimp_PresentArmFollowsTarget()
        {
            var input = PartInputs.Idle;
            input.ArmLeft.HandTarget = new Vector3(-0.3f, 1f, 0.5f);
            input.ArmLeft.Grab = true;
            input.ArmRight.Grab = true;
            var present = BodyPart.All & ~BodyPart.ArmRight;

            var intent = TrenchcoatIntentMixer.Mix(input, present, 0f, Settings);

            Assert.IsFalse(intent.LeftArmLimp);
            Assert.AreEqual(input.ArmLeft.HandTarget, intent.LeftHandTarget);
            Assert.IsTrue(intent.LeftGrab);
            Assert.IsTrue(intent.RightArmLimp);
            Assert.IsFalse(intent.RightGrab, "a limp arm cannot grab");
        }

        [Test]
        public void Mix_MissingHead_Slumps_IgnoresLook()
        {
            var input = PartInputs.Idle;
            input.Head.Yaw = 45f;
            var intent = TrenchcoatIntentMixer.Mix(input, BodyPart.All & ~BodyPart.Head, 0f, Settings);
            Assert.IsTrue(intent.HeadSlumped);
            Assert.AreEqual(0f, intent.HeadYaw, Eps);

            var withHead = TrenchcoatIntentMixer.Mix(input, BodyPart.All, 0f, Settings);
            Assert.IsFalse(withHead.HeadSlumped);
            Assert.AreEqual(45f, withHead.HeadYaw, Eps);
        }
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `tools/unity-test.sh TrenchcoatIntentMixerTests`
Expected: exit 1, log con `error CS0246` sobre `MixerSettings` / `PartInputs`.

- [ ] **Step 3: Implementar**

`TrashPandas/Assets/Scripts/Core/Trenchcoat/PartInputs.cs`:
```csharp
using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    public struct LegInput
    {
        /// <summary>-1..1, forward push of this leg.</summary>
        public float Drive;
        /// <summary>-1..1, positive = right.</summary>
        public float Steer;
        /// <summary>Time (s) of the last jump press; NegativeInfinity = never.</summary>
        public float JumpPressedAt;
        public bool Crouch;

        public static LegInput Idle => new LegInput { JumpPressedAt = float.NegativeInfinity };
    }

    public struct ArmInput
    {
        /// <summary>Hand target in body-local space.</summary>
        public Vector3 HandTarget;
        public bool Grab;
    }

    public struct HeadInput
    {
        public float Yaw;
        public float Pitch;
    }

    public struct PartInputs
    {
        public LegInput LegLeft;
        public LegInput LegRight;
        public ArmInput ArmLeft;
        public ArmInput ArmRight;
        public HeadInput Head;

        public static PartInputs Idle => new PartInputs { LegLeft = LegInput.Idle, LegRight = LegInput.Idle };
    }
}
```

`TrashPandas/Assets/Scripts/Core/Trenchcoat/BodyIntent.cs`:
```csharp
using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>What the trenchcoat body should do this frame, after mixing every slot's input.</summary>
    public struct BodyIntent
    {
        public float Forward;     // -1..1
        public float Turn;        // -1..1, positive = right
        public bool Jump;
        public bool Crouch;
        public bool Collapsed;    // no legs: the body slumps down

        public bool LeftArmLimp;
        public bool RightArmLimp;
        public Vector3 LeftHandTarget;   // body-local
        public Vector3 RightHandTarget;  // body-local
        public bool LeftGrab;
        public bool RightGrab;

        public bool HeadSlumped;
        public float HeadYaw;
        public float HeadPitch;
    }
}
```

`TrashPandas/Assets/Scripts/Core/Trenchcoat/TrenchcoatIntentMixer.cs`:
```csharp
using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    public sealed class MixerSettings
    {
        /// <summary>Max seconds between both legs' jump presses, and since the latest press.</summary>
        public float JumpWindow = 0.25f;
        /// <summary>How much a drive difference between legs turns the body.</summary>
        public float DesyncTurnFactor = 1f;
        /// <summary>Speed multiplier when only one leg is present.</summary>
        public float LimpSpeedFactor = 0.4f;
        /// <summary>Turn drift toward the missing leg, scaled by speed.</summary>
        public float LimpTurnBias = 0.3f;
    }

    /// <summary>Combines per-part inputs into one body intent. All comedy rules live here.</summary>
    public static class TrenchcoatIntentMixer
    {
        public static BodyIntent Mix(in PartInputs input, BodyPart present, float now, MixerSettings settings)
        {
            var intent = new BodyIntent();
            MixLegs(in input, present, now, settings, ref intent);
            MixArms(in input, present, ref intent);

            if ((present & BodyPart.Head) != 0)
            {
                intent.HeadYaw = Sanitize(input.Head.Yaw, 180f);
                intent.HeadPitch = Sanitize(input.Head.Pitch, 90f);
            }
            else
            {
                intent.HeadSlumped = true;
            }
            return intent;
        }

        static void MixLegs(in PartInputs input, BodyPart present, float now, MixerSettings s, ref BodyIntent intent)
        {
            bool hasLeft = (present & BodyPart.LegLeft) != 0;
            bool hasRight = (present & BodyPart.LegRight) != 0;

            if (hasLeft && hasRight)
            {
                float driveL = Unit(input.LegLeft.Drive);
                float driveR = Unit(input.LegRight.Drive);
                float steer = (Unit(input.LegLeft.Steer) + Unit(input.LegRight.Steer)) * 0.5f;
                intent.Forward = (driveL + driveR) * 0.5f;
                intent.Turn = Mathf.Clamp(steer + (driveL - driveR) * s.DesyncTurnFactor, -1f, 1f);
                intent.Jump = IsCoordinatedJump(input.LegLeft.JumpPressedAt, input.LegRight.JumpPressedAt, now, s.JumpWindow);
                intent.Crouch = input.LegLeft.Crouch || input.LegRight.Crouch;
            }
            else if (hasLeft || hasRight)
            {
                var leg = hasLeft ? input.LegLeft : input.LegRight;
                intent.Forward = Unit(leg.Drive) * s.LimpSpeedFactor;
                float towardMissing = hasLeft ? 1f : -1f; // missing right leg drags right
                float drift = towardMissing * s.LimpTurnBias * Mathf.Abs(intent.Forward);
                intent.Turn = Mathf.Clamp(Unit(leg.Steer) + drift, -1f, 1f);
                intent.Crouch = leg.Crouch;
            }
            else
            {
                intent.Collapsed = true;
            }
        }

        static void MixArms(in PartInputs input, BodyPart present, ref BodyIntent intent)
        {
            if ((present & BodyPart.ArmLeft) != 0)
            {
                intent.LeftHandTarget = input.ArmLeft.HandTarget;
                intent.LeftGrab = input.ArmLeft.Grab;
            }
            else
            {
                intent.LeftArmLimp = true;
            }

            if ((present & BodyPart.ArmRight) != 0)
            {
                intent.RightHandTarget = input.ArmRight.HandTarget;
                intent.RightGrab = input.ArmRight.Grab;
            }
            else
            {
                intent.RightArmLimp = true;
            }
        }

        static bool IsCoordinatedJump(float left, float right, float now, float window)
        {
            if (float.IsInfinity(left) || float.IsInfinity(right) || float.IsNaN(left) || float.IsNaN(right))
                return false;
            float latest = Mathf.Max(left, right);
            return Mathf.Abs(left - right) <= window && now >= latest && now - latest <= window;
        }

        static float Unit(float value) => Sanitize(value, 1f);

        static float Sanitize(float value, float limit) =>
            float.IsNaN(value) ? 0f : Mathf.Clamp(value, -limit, limit);
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `tools/unity-test.sh TrenchcoatIntentMixerTests`
Expected: `total="11" passed="11" failed="0"`

- [ ] **Step 5: Commit**

```bash
git add TrashPandas/Assets/Scripts/Core TrashPandas/Assets/Tests/EditMode
git commit -m "feat(core): add trenchcoat intent mixer with limp, collapse and coordinated jump

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Enrutar el input de cada jugador a sus partes (`SlotInputRouter`)

**Files:**
- Create: `TrashPandas/Assets/Scripts/Core/Trenchcoat/SlotInput.cs`
- Create: `TrashPandas/Assets/Scripts/Core/Trenchcoat/SlotInputRouter.cs`
- Test: `TrashPandas/Assets/Tests/EditMode/SlotInputRouterTests.cs`

**Interfaces:**
- Consumes: `SlotSystem` (`SlotCount`, `PartsOf`, `OccupantOf`), `PartInputs`, `LegInput`, `ArmInput`, `HeadInput`.
- Produces:
  ```csharp
  public struct SlotInput { public Vector2 Move; public bool Crouch; public float JumpPressedAt;
      public Vector3 HandTarget; public bool PrimaryGrab; public bool SecondaryGrab; public Vector2 Look;
      public static SlotInput Idle { get; } }
  public static class SlotInputRouter {
      public const float ArmSpread = 0.25f;
      public static PartInputs Route(SlotSystem slots, IReadOnlyDictionary<int, SlotInput> inputsByPlayer);
  }
  ```
  Reglas: `Move.y → Drive`, `Move.x → Steer` en cada pierna del puesto. Si un puesto tiene los dos brazos, la mano izquierda va en `HandTarget + (-ArmSpread,0,0)` con `PrimaryGrab` y la derecha en `HandTarget + (ArmSpread,0,0)` con `SecondaryGrab`; si tiene un solo brazo, usa `HandTarget` exacto y `PrimaryGrab`. `Look.x → Yaw`, `Look.y → Pitch`. Un puesto vacío o sin input recibe valores en reposo.

- [ ] **Step 1: Escribir el test que falla**

`TrashPandas/Assets/Tests/EditMode/SlotInputRouterTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class SlotInputRouterTests
    {
        [Test]
        public void Route_LegsSlot_DrivesBothLegsWithSameInput()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0); // Legs
            var inputs = new Dictionary<int, SlotInput>
            {
                [1] = new SlotInput { Move = new Vector2(0.5f, 1f), JumpPressedAt = 3f, Crouch = true },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(1f, parts.LegLeft.Drive);
            Assert.AreEqual(1f, parts.LegRight.Drive);
            Assert.AreEqual(0.5f, parts.LegLeft.Steer);
            Assert.AreEqual(3f, parts.LegLeft.JumpPressedAt);
            Assert.AreEqual(3f, parts.LegRight.JumpPressedAt);
            Assert.IsTrue(parts.LegRight.Crouch);
        }

        [Test]
        public void Route_SeparateLegSlots_EachLegOwnInput()
        {
            var slots = new SlotSystem(4);
            slots.TryEnter(1, 0); // LegLeft
            slots.TryEnter(2, 1); // LegRight
            var inputs = new Dictionary<int, SlotInput>
            {
                [1] = new SlotInput { Move = new Vector2(0f, 1f), JumpPressedAt = float.NegativeInfinity },
                [2] = new SlotInput { Move = new Vector2(0f, 0.2f), JumpPressedAt = float.NegativeInfinity },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(1f, parts.LegLeft.Drive);
            Assert.AreEqual(0.2f, parts.LegRight.Drive);
        }

        [Test]
        public void Route_BothArmsSlot_SpreadsHandsAndSplitsGrabs()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 1); // Arms
            var target = new Vector3(0f, 1f, 0.5f);
            var inputs = new Dictionary<int, SlotInput>
            {
                [1] = new SlotInput { HandTarget = target, PrimaryGrab = true, SecondaryGrab = false },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(target + new Vector3(-SlotInputRouter.ArmSpread, 0f, 0f), parts.ArmLeft.HandTarget);
            Assert.AreEqual(target + new Vector3(SlotInputRouter.ArmSpread, 0f, 0f), parts.ArmRight.HandTarget);
            Assert.IsTrue(parts.ArmLeft.Grab);
            Assert.IsFalse(parts.ArmRight.Grab);
        }

        [Test]
        public void Route_SingleArmSlot_UsesExactTargetAndPrimaryGrab()
        {
            var slots = new SlotSystem(5);
            slots.TryEnter(9, 3); // ArmRight
            var target = new Vector3(0.4f, 1.2f, 0.6f);
            var inputs = new Dictionary<int, SlotInput>
            {
                [9] = new SlotInput { HandTarget = target, PrimaryGrab = true },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(target, parts.ArmRight.HandTarget);
            Assert.IsTrue(parts.ArmRight.Grab);
            Assert.IsFalse(parts.ArmLeft.Grab);
        }

        [Test]
        public void Route_UpperSlotOfTwoPlayers_ControlsArmsAndHead()
        {
            var slots = new SlotSystem(2);
            slots.TryEnter(1, 1); // Arms | Head
            var inputs = new Dictionary<int, SlotInput>
            {
                [1] = new SlotInput { Look = new Vector2(30f, -10f), PrimaryGrab = true },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(30f, parts.Head.Yaw);
            Assert.AreEqual(-10f, parts.Head.Pitch);
            Assert.IsTrue(parts.ArmLeft.Grab);
        }

        [Test]
        public void Route_EmptySlotsAndMissingInputs_StayIdle()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0); // Legs occupied, but player 1 sent no input
            var parts = SlotInputRouter.Route(slots, new Dictionary<int, SlotInput>());

            Assert.AreEqual(0f, parts.LegLeft.Drive);
            Assert.IsTrue(float.IsNegativeInfinity(parts.LegLeft.JumpPressedAt));
            Assert.IsTrue(float.IsNegativeInfinity(parts.LegRight.JumpPressedAt));
        }
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `tools/unity-test.sh SlotInputRouterTests`
Expected: exit 1, log con `error CS0246` sobre `SlotInput`.

- [ ] **Step 3: Implementar**

`TrashPandas/Assets/Scripts/Core/Trenchcoat/SlotInput.cs`:
```csharp
using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>Raw input one player sends from their slot. Unused fields are ignored by the router.</summary>
    public struct SlotInput
    {
        public Vector2 Move;          // legs: x = steer, y = drive
        public bool Crouch;
        public float JumpPressedAt;   // NegativeInfinity = never
        public Vector3 HandTarget;    // arms, body-local
        public bool PrimaryGrab;      // left hand (or the only hand)
        public bool SecondaryGrab;    // right hand when the slot has both arms
        public Vector2 Look;          // head: x = yaw, y = pitch (degrees)

        public static SlotInput Idle => new SlotInput { JumpPressedAt = float.NegativeInfinity };
    }
}
```

`TrashPandas/Assets/Scripts/Core/Trenchcoat/SlotInputRouter.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>Distributes each occupant's slot input to the body parts their slot controls.</summary>
    public static class SlotInputRouter
    {
        public const float ArmSpread = 0.25f;

        public static PartInputs Route(SlotSystem slots, IReadOnlyDictionary<int, SlotInput> inputsByPlayer)
        {
            var parts = PartInputs.Idle;
            for (int i = 0; i < slots.SlotCount; i++)
            {
                var occupant = slots.OccupantOf(i);
                if (!occupant.HasValue || !inputsByPlayer.TryGetValue(occupant.Value, out var input))
                    continue;
                Apply(slots.PartsOf(i), input, ref parts);
            }
            return parts;
        }

        static void Apply(BodyPart slotParts, SlotInput input, ref PartInputs parts)
        {
            var leg = new LegInput
            {
                Drive = input.Move.y,
                Steer = input.Move.x,
                JumpPressedAt = input.JumpPressedAt,
                Crouch = input.Crouch,
            };
            if ((slotParts & BodyPart.LegLeft) != 0) parts.LegLeft = leg;
            if ((slotParts & BodyPart.LegRight) != 0) parts.LegRight = leg;

            bool bothArms = (slotParts & BodyPart.Arms) == BodyPart.Arms;
            if (bothArms)
            {
                parts.ArmLeft = new ArmInput { HandTarget = input.HandTarget + new Vector3(-ArmSpread, 0f, 0f), Grab = input.PrimaryGrab };
                parts.ArmRight = new ArmInput { HandTarget = input.HandTarget + new Vector3(ArmSpread, 0f, 0f), Grab = input.SecondaryGrab };
            }
            else if ((slotParts & BodyPart.ArmLeft) != 0)
            {
                parts.ArmLeft = new ArmInput { HandTarget = input.HandTarget, Grab = input.PrimaryGrab };
            }
            else if ((slotParts & BodyPart.ArmRight) != 0)
            {
                parts.ArmRight = new ArmInput { HandTarget = input.HandTarget, Grab = input.PrimaryGrab };
            }

            if ((slotParts & BodyPart.Head) != 0)
                parts.Head = new HeadInput { Yaw = input.Look.x, Pitch = input.Look.y };
        }
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `tools/unity-test.sh SlotInputRouterTests`
Expected: `total="6" passed="6" failed="0"`

- [ ] **Step 5: Commit**

```bash
git add TrashPandas/Assets/Scripts/Core TrashPandas/Assets/Tests/EditMode
git commit -m "feat(core): route each player's slot input to their body parts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Modelo de posesión del modo debug

Una persona controla todos los puestos. Cada puesto tiene un jugador virtual (ids `0..n-1`); la persona "posee" uno a la vez.

**Files:**
- Create: `TrashPandas/Assets/Scripts/Core/Debugging/DebugPossessionModel.cs`
- Test: `TrashPandas/Assets/Tests/EditMode/DebugPossessionModelTests.cs`

**Interfaces:**
- Consumes: `SlotSystem` (`SlotCount`, `TryEnter`, `Leave`, `SlotOf`, `FirstFreeSlot`).
- Produces:
  ```csharp
  namespace TrashPandas.Core.Debugging
  public sealed class DebugPossessionModel {
      public DebugPossessionModel(SlotSystem slots);   // llena cada puesto i con el jugador virtual i
      public int ActivePlayerId { get; }               // empieza en 0
      public bool ActiveIsOutside { get; }
      public bool CycleNext();      // Tab: siguiente jugador que esté DENTRO; no hace nada si el activo está afuera
      public bool LeaveCoat();      // E dentro: el activo sale; true si salió
      public bool ReturnToCoat();   // E afuera: el activo entra al primer puesto libre; true si entró
  }
  ```

- [ ] **Step 1: Escribir el test que falla**

`TrashPandas/Assets/Tests/EditMode/DebugPossessionModelTests.cs`:
```csharp
using NUnit.Framework;
using TrashPandas.Core.Debugging;
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Tests
{
    public class DebugPossessionModelTests
    {
        [Test]
        public void New_FillsEverySlot_ActiveIsPlayerZeroInside()
        {
            var slots = new SlotSystem(3);
            var model = new DebugPossessionModel(slots);

            Assert.AreEqual(BodyPart.None, slots.MissingParts);
            Assert.AreEqual(0, model.ActivePlayerId);
            Assert.IsFalse(model.ActiveIsOutside);
        }

        [Test]
        public void CycleNext_WrapsAroundAllInsidePlayers()
        {
            var model = new DebugPossessionModel(new SlotSystem(3));
            model.CycleNext(); Assert.AreEqual(1, model.ActivePlayerId);
            model.CycleNext(); Assert.AreEqual(2, model.ActivePlayerId);
            model.CycleNext(); Assert.AreEqual(0, model.ActivePlayerId);
        }

        [Test]
        public void LeaveCoat_MakesActiveOutside_AndPartMissing()
        {
            var slots = new SlotSystem(3);
            var model = new DebugPossessionModel(slots);
            model.CycleNext(); // player 1 = Arms

            Assert.IsTrue(model.LeaveCoat());
            Assert.IsTrue(model.ActiveIsOutside);
            Assert.AreEqual(BodyPart.Arms, slots.MissingParts);
            Assert.IsFalse(model.LeaveCoat(), "leaving twice is a no-op");
        }

        [Test]
        public void CycleNext_WhileOutside_DoesNothing()
        {
            var model = new DebugPossessionModel(new SlotSystem(3));
            model.LeaveCoat();
            Assert.IsFalse(model.CycleNext());
            Assert.AreEqual(0, model.ActivePlayerId);
            Assert.IsTrue(model.ActiveIsOutside);
        }

        [Test]
        public void ReturnToCoat_TakesFirstFreeSlot()
        {
            var slots = new SlotSystem(3);
            var model = new DebugPossessionModel(slots);
            model.LeaveCoat(); // player 0 frees slot 0

            Assert.IsTrue(model.ReturnToCoat());
            Assert.IsFalse(model.ActiveIsOutside);
            Assert.AreEqual(0, slots.SlotOf(0));
            Assert.AreEqual(BodyPart.None, slots.MissingParts);
        }

        [Test]
        public void ReturnToCoat_WhenFull_Fails()
        {
            var slots = new SlotSystem(2);
            var model = new DebugPossessionModel(slots);
            Assert.IsFalse(model.ReturnToCoat(), "already inside and coat full");
            Assert.AreEqual(0, slots.SlotOf(0));
            Assert.AreEqual(1, slots.SlotOf(1));
        }
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `tools/unity-test.sh DebugPossessionModelTests`
Expected: exit 1, log con `error CS0234` / `CS0246` sobre `TrashPandas.Core.Debugging`.

- [ ] **Step 3: Implementar**

`TrashPandas/Assets/Scripts/Core/Debugging/DebugPossessionModel.cs`:
```csharp
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Core.Debugging
{
    /// <summary>
    /// Debug mode: one person drives every slot. Each slot starts with virtual player i;
    /// the person possesses one player at a time.
    /// </summary>
    public sealed class DebugPossessionModel
    {
        readonly SlotSystem _slots;

        public DebugPossessionModel(SlotSystem slots)
        {
            _slots = slots;
            for (int i = 0; i < slots.SlotCount; i++)
                slots.TryEnter(i, i);
            ActivePlayerId = 0;
        }

        public int ActivePlayerId { get; private set; }

        public bool ActiveIsOutside => !_slots.SlotOf(ActivePlayerId).HasValue;

        public bool CycleNext()
        {
            if (ActiveIsOutside) return false;
            int count = _slots.SlotCount;
            for (int step = 1; step < count; step++)
            {
                int candidate = (ActivePlayerId + step) % count;
                if (_slots.SlotOf(candidate).HasValue)
                {
                    ActivePlayerId = candidate;
                    return true;
                }
            }
            return false;
        }

        public bool LeaveCoat() => _slots.Leave(ActivePlayerId);

        public bool ReturnToCoat()
        {
            if (!ActiveIsOutside) return false;
            var free = _slots.FirstFreeSlot();
            return free.HasValue && _slots.TryEnter(ActivePlayerId, free.Value);
        }
    }
}
```

- [ ] **Step 4: Correr la suite completa y verificar que pasa**

Run: `tools/unity-test.sh`
Expected: `total="42" passed="42" failed="0"` (1 smoke + 8 layout + 10 slots + 11 mixer + 6 router + 6 possession)

- [ ] **Step 5: Commit**

```bash
git add TrashPandas/Assets/Scripts/Core TrashPandas/Assets/Tests/EditMode
git commit -m "feat(core): add debug possession model for single-person testing

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Comportamientos en runtime (cuerpo, mapache, cámara, input y controlador)

Estos `MonoBehaviour` son "tontos": leen input, llaman a Core y mueven transforms. Se verifican en la escena de la Task 8. Aquí solo se verifica que compilen (la suite sigue en verde).

**Files:**
- Create: `TrashPandas/Assets/Scripts/Runtime/Input/DebugInputReader.cs`
- Create: `TrashPandas/Assets/Scripts/Runtime/Trenchcoat/TrenchcoatBody.cs`
- Create: `TrashPandas/Assets/Scripts/Runtime/Raccoon/Climbable.cs`
- Create: `TrashPandas/Assets/Scripts/Runtime/Raccoon/RaccoonController.cs`
- Create: `TrashPandas/Assets/Scripts/Runtime/Cameras/FollowCamera.cs`
- Create: `TrashPandas/Assets/Scripts/Runtime/Trenchcoat/TrenchcoatController.cs`

**Interfaces:**
- Consumes: `SlotSystem`, `SlotInput`, `SlotInputRouter.Route`, `TrenchcoatIntentMixer.Mix`, `MixerSettings`, `BodyIntent`, `DebugPossessionModel`.
- Produces (los usa `GreyboxSceneBuilder` en la Task 8, con campos públicos para cablearlos desde código):
  - `TrenchcoatBody` con `public Transform Torso, Head, LeftHand, RightHand;` y `public void SetIntent(BodyIntent intent)`.
  - `RaccoonController` (requiere `CharacterController`) con `public Transform CameraTransform;` y `public void SetInput(Vector2 move, bool jumpPressed, bool crouchHeld)`.
  - `FollowCamera` con `public Transform Target;`.
  - `TrenchcoatController` con `public int PlayerCount = 3; public TrenchcoatBody Body; public RaccoonController RaccoonPrefab; public FollowCamera Camera;`.
  - `DebugInputReader` (clase C# normal, no `MonoBehaviour`).

- [ ] **Step 1: Escribir `DebugInputReader`**

`TrashPandas/Assets/Scripts/Runtime/Input/DebugInputReader.cs`:
```csharp
using TrashPandas.Core.Trenchcoat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrashPandas.Runtime.Input
{
    /// <summary>Keyboard + mouse for the single debug person. Replaced by per-player input in stage 2.</summary>
    public sealed class DebugInputReader
    {
        const float LookSpeed = 90f; // degrees per second with arrow keys
        float _lastJumpPressedAt = float.NegativeInfinity;
        Vector2 _look;

        public bool TogglePressed => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        public bool CyclePressed => Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;

        public Vector2 Move()
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            var move = new Vector2(
                (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f),
                (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f));
            return Vector2.ClampMagnitude(move, 1f);
        }

        public bool JumpPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        public bool CrouchHeld => Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;

        public SlotInput ReadSlotInput(float now, float deltaTime)
        {
            if (JumpPressed) _lastJumpPressedAt = now;

            var k = Keyboard.current;
            if (k != null)
            {
                _look.x += ((k.rightArrowKey.isPressed ? 1f : 0f) - (k.leftArrowKey.isPressed ? 1f : 0f)) * LookSpeed * deltaTime;
                _look.y += ((k.upArrowKey.isPressed ? 1f : 0f) - (k.downArrowKey.isPressed ? 1f : 0f)) * LookSpeed * deltaTime;
                _look.x = Mathf.Clamp(_look.x, -80f, 80f);
                _look.y = Mathf.Clamp(_look.y, -40f, 40f);
            }

            var mouse = Mouse.current;
            Vector3 hand = new Vector3(0f, 1.2f, 0.5f);
            bool primary = false, secondary = false;
            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                float vx = Mathf.Clamp01(p.x / Mathf.Max(1, Screen.width));
                float vy = Mathf.Clamp01(p.y / Mathf.Max(1, Screen.height));
                hand = new Vector3((vx - 0.5f) * 1.2f, 0.6f + vy * 1.2f, 0.5f);
                primary = mouse.leftButton.isPressed;
                secondary = mouse.rightButton.isPressed;
            }

            return new SlotInput
            {
                Move = Move(),
                Crouch = CrouchHeld,
                JumpPressedAt = _lastJumpPressedAt,
                HandTarget = hand,
                PrimaryGrab = primary,
                SecondaryGrab = secondary,
                Look = _look,
            };
        }
    }
}
```

- [ ] **Step 2: Escribir `TrenchcoatBody`**

`TrashPandas/Assets/Scripts/Runtime/Trenchcoat/TrenchcoatBody.cs`:
```csharp
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Runtime.Trenchcoat
{
    /// <summary>Greybox trenchcoat: a wobbly Rigidbody capsule with procedural head and hands.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TrenchcoatBody : MonoBehaviour
    {
        public Transform Torso;
        public Transform Head;
        public Transform LeftHand;
        public Transform RightHand;

        public float MoveSpeed = 2.2f;
        public float TurnSpeed = 140f;
        public float JumpVelocity = 4.5f;
        public float JumpCooldown = 0.6f;
        public float WobbleAmount = 6f;
        public float HandSpeed = 8f;

        static readonly Vector3 LeftShoulder = new Vector3(-0.35f, 1.35f, 0f);
        static readonly Vector3 RightShoulder = new Vector3(0.35f, 1.35f, 0f);
        static readonly Vector3 HeadRest = new Vector3(0f, 1.75f, 0f);
        static readonly Vector3 TorsoRest = new Vector3(0f, 0.9f, 0f);

        Rigidbody _rb;
        BodyIntent _intent;
        float _lastJumpTime = -10f;
        float _wobblePhase;

        public void SetIntent(BodyIntent intent) => _intent = intent;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            float speed = _intent.Crouch ? MoveSpeed * 0.5f : MoveSpeed;
            Vector3 planar = transform.forward * (_intent.Forward * speed);
            _rb.linearVelocity = new Vector3(planar.x, _rb.linearVelocity.y, planar.z);
            _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, _intent.Turn * TurnSpeed * Time.fixedDeltaTime, 0f));

            if (_intent.Jump && IsGrounded() && Time.time - _lastJumpTime > JumpCooldown)
            {
                _lastJumpTime = Time.time;
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, JumpVelocity, _rb.linearVelocity.z);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _wobblePhase += dt * (2f + Mathf.Abs(_intent.Forward) * 8f);

            // Torso: collapses when no legs, wobbles side to side while walking.
            Vector3 torsoPos = _intent.Collapsed ? TorsoRest + Vector3.down * 0.5f
                             : _intent.Crouch ? TorsoRest + Vector3.down * 0.25f
                             : TorsoRest;
            float sway = Mathf.Sin(_wobblePhase) * WobbleAmount * (0.3f + Mathf.Abs(_intent.Forward));
            float lean = _intent.Collapsed ? 25f : _intent.Forward * 8f;
            Torso.localPosition = Vector3.Lerp(Torso.localPosition, torsoPos, dt * 6f);
            Torso.localRotation = Quaternion.Slerp(Torso.localRotation, Quaternion.Euler(lean, 0f, sway), dt * 6f);

            // Head: follows look, or slumps sideways.
            Quaternion headRot = _intent.HeadSlumped
                ? Quaternion.Euler(20f, 0f, 60f)
                : Quaternion.Euler(-_intent.HeadPitch, _intent.HeadYaw, 0f);
            Head.localPosition = Vector3.Lerp(Head.localPosition, torsoPos - TorsoRest + HeadRest, dt * 6f);
            Head.localRotation = Quaternion.Slerp(Head.localRotation, headRot, dt * 5f);

            // Hands: reach for targets, or dangle like noodles.
            float dangle = Mathf.Sin(_wobblePhase * 1.3f) * 0.12f;
            Vector3 drop = torsoPos - TorsoRest;
            Vector3 left = _intent.LeftArmLimp ? LeftShoulder + drop + new Vector3(-0.05f, -0.75f, dangle) : _intent.LeftHandTarget;
            Vector3 right = _intent.RightArmLimp ? RightShoulder + drop + new Vector3(0.05f, -0.75f, -dangle) : _intent.RightHandTarget;
            LeftHand.localPosition = Vector3.Lerp(LeftHand.localPosition, left, dt * HandSpeed);
            RightHand.localPosition = Vector3.Lerp(RightHand.localPosition, right, dt * HandSpeed);
            LeftHand.localScale = Vector3.one * (_intent.LeftGrab ? 0.13f : 0.18f);
            RightHand.localScale = Vector3.one * (_intent.RightGrab ? 0.13f : 0.18f);
        }

        bool IsGrounded() =>
            Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f, ~0, QueryTriggerInteraction.Ignore);
    }
}
```

- [ ] **Step 3: Escribir `Climbable` y `RaccoonController`**

`TrashPandas/Assets/Scripts/Runtime/Raccoon/Climbable.cs`:
```csharp
using UnityEngine;

namespace TrashPandas.Runtime.Raccoon
{
    /// <summary>Marks a collider a loose raccoon can climb (curtains, tablecloths, hedges).</summary>
    public sealed class Climbable : MonoBehaviour { }
}
```

`TrashPandas/Assets/Scripts/Runtime/Raccoon/RaccoonController.cs`:
```csharp
using UnityEngine;

namespace TrashPandas.Runtime.Raccoon
{
    /// <summary>A loose raccoon: runs, jumps, climbs Climbable surfaces, crouches under tables.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class RaccoonController : MonoBehaviour
    {
        public Transform CameraTransform;
        public float RunSpeed = 4.5f;
        public float JumpVelocity = 5.5f;
        public float Gravity = -20f;
        public float ClimbSpeed = 2.5f;
        public float StandHeight = 0.6f;
        public float CrouchHeight = 0.3f;

        CharacterController _cc;
        Vector2 _move;
        bool _jumpPressed;
        bool _crouchHeld;
        float _verticalVelocity;

        public void SetInput(Vector2 move, bool jumpPressed, bool crouchHeld)
        {
            _move = move;
            _jumpPressed |= jumpPressed;   // latched until consumed in Update
            _crouchHeld = crouchHeld;
        }

        void Awake() => _cc = GetComponent<CharacterController>();

        void Update()
        {
            float height = _crouchHeld ? CrouchHeight : StandHeight;
            _cc.height = height;
            _cc.center = new Vector3(0f, height * 0.5f, 0f);

            Vector3 forward = CameraTransform ? Vector3.ProjectOnPlane(CameraTransform.forward, Vector3.up).normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 wish = (forward * _move.y + right * _move.x);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            bool climbing = _move.y > 0.1f && IsFacingClimbable(wish);
            if (climbing)
            {
                _verticalVelocity = ClimbSpeed;
            }
            else if (_cc.isGrounded)
            {
                _verticalVelocity = _jumpPressed ? JumpVelocity : -1f;
            }
            else
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
            _jumpPressed = false;

            float speed = _crouchHeld ? RunSpeed * 0.5f : RunSpeed;
            _cc.Move((wish * speed + Vector3.up * _verticalVelocity) * Time.deltaTime);
            if (wish.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wish), Time.deltaTime * 12f);
        }

        bool IsFacingClimbable(Vector3 wish)
        {
            if (wish.sqrMagnitude < 0.01f) return false;
            Vector3 origin = transform.position + Vector3.up * (_cc.height * 0.5f);
            return Physics.Raycast(origin, wish.normalized, out var hit, _cc.radius + 0.15f, ~0, QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponentInParent<Climbable>() != null;
        }
    }
}
```

- [ ] **Step 4: Escribir `FollowCamera`**

`TrashPandas/Assets/Scripts/Runtime/Cameras/FollowCamera.cs`:
```csharp
using UnityEngine;

namespace TrashPandas.Runtime.Cameras
{
    /// <summary>Third-person follow camera. Distance scales with the target's size.</summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 5f;
        public float Height = 3f;
        public float Smooth = 6f;

        public void Follow(Transform target, float distance, float height)
        {
            Target = target;
            Distance = distance;
            Height = height;
        }

        void LateUpdate()
        {
            if (!Target) return;
            Vector3 back = -Vector3.ProjectOnPlane(Target.forward, Vector3.up).normalized;
            Vector3 desired = Target.position + back * Distance + Vector3.up * Height;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * Smooth);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(Target.position + Vector3.up * (Height * 0.3f) - transform.position), Time.deltaTime * Smooth);
        }
    }
}
```

- [ ] **Step 5: Escribir `TrenchcoatController`** (orquesta todo y dibuja un HUD de depuración)

`TrashPandas/Assets/Scripts/Runtime/Trenchcoat/TrenchcoatController.cs`:
```csharp
using System.Collections.Generic;
using TrashPandas.Core.Debugging;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Input;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Trenchcoat
{
    /// <summary>Debug-mode orchestrator: one person drives every slot, can hop out as a raccoon and back.</summary>
    public sealed class TrenchcoatController : MonoBehaviour
    {
        public int PlayerCount = 3;
        public TrenchcoatBody Body;
        public RaccoonController RaccoonPrefab;
        public FollowCamera Camera;
        public float ReturnDistance = 1.6f;

        readonly MixerSettings _mixer = new MixerSettings();
        readonly Dictionary<int, SlotInput> _inputs = new Dictionary<int, SlotInput>();
        readonly DebugInputReader _reader = new DebugInputReader();
        SlotSystem _slots;
        DebugPossessionModel _possession;
        RaccoonController _raccoon;
        string _status = "";

        void Awake()
        {
            _slots = new SlotSystem(Mathf.Clamp(PlayerCount, SlotLayout.MinPlayers, SlotLayout.MaxPlayers));
            _possession = new DebugPossessionModel(_slots);
            Camera.Follow(Body.transform, 5f, 3f);
        }

        void Update()
        {
            if (_reader.CyclePressed) _possession.CycleNext();
            if (_reader.TogglePressed) Toggle();

            _inputs.Clear();
            if (_possession.ActiveIsOutside)
            {
                _raccoon.SetInput(_reader.Move(), _reader.JumpPressed, _reader.CrouchHeld);
            }
            else
            {
                _inputs[_possession.ActivePlayerId] = _reader.ReadSlotInput(Time.time, Time.deltaTime);
            }

            var parts = SlotInputRouter.Route(_slots, _inputs);
            Body.SetIntent(TrenchcoatIntentMixer.Mix(parts, _slots.ControlledParts, Time.time, _mixer));
        }

        void Toggle()
        {
            if (!_possession.ActiveIsOutside)
            {
                if (!_possession.LeaveCoat()) return;
                Vector3 spawn = Body.transform.position + Body.transform.right * 0.9f + Vector3.up * 0.2f;
                _raccoon = Instantiate(RaccoonPrefab, spawn, Body.transform.rotation);
                _raccoon.CameraTransform = Camera.transform;
                Camera.Follow(_raccoon.transform, 2.5f, 1.4f);
                _status = "";
                return;
            }

            float distance = Vector3.Distance(_raccoon.transform.position, Body.transform.position);
            if (distance > ReturnDistance) { _status = "Too far from the coat"; return; }
            if (!_possession.ReturnToCoat()) { _status = "No free slot"; return; }

            Destroy(_raccoon.gameObject);
            _raccoon = null;
            Camera.Follow(Body.transform, 5f, 3f);
            _status = "";
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 360, 260), GUI.skin.box);
            GUILayout.Label($"DEBUG — {_slots.SlotCount} players   [Tab] switch  [E] out/in");
            for (int i = 0; i < _slots.SlotCount; i++)
            {
                var occupant = _slots.OccupantOf(i);
                string who = occupant.HasValue ? $"P{occupant.Value}" : "— EMPTY —";
                string me = occupant == _possession.ActivePlayerId ? "  ◀ YOU" : "";
                GUILayout.Label($"Slot {i} [{_slots.PartsOf(i)}]: {who}{me}");
            }
            GUILayout.Label($"Missing: {_slots.MissingParts}");
            if (_possession.ActiveIsOutside) GUILayout.Label($"P{_possession.ActivePlayerId} is a loose raccoon");
            if (_status.Length > 0) GUILayout.Label(_status);
            GUILayout.EndArea();
        }
    }
}
```

- [ ] **Step 6: Verificar que compila**

Run: `tools/unity-test.sh`
Expected: `total="42" passed="42" failed="0"`. Si sale "No test results produced", revisar `.test-results/editmode.log` buscando `error CS`.

- [ ] **Step 7: Commit**

```bash
git add TrashPandas/Assets/Scripts/Runtime
git commit -m "feat(runtime): add greybox trenchcoat body, raccoon, camera and debug controller

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Escena gris generada por código y prueba de juego

**Files:**
- Create: `TrashPandas/Assets/Scripts/Editor/GreyboxSceneBuilder.cs`
- Generated: `TrashPandas/Assets/Scenes/Greybox_Trenchcoat.unity`, `TrashPandas/Assets/Prefabs/Raccoon.prefab`, `TrashPandas/Assets/Materials/Greybox/*.mat`

**Interfaces:**
- Consumes: `TrenchcoatBody` (`Torso`, `Head`, `LeftHand`, `RightHand`), `RaccoonController`, `Climbable`, `FollowCamera`, `TrenchcoatController` (`PlayerCount`, `Body`, `RaccoonPrefab`, `Camera`).
- Produces: método `TrashPandas.EditorTools.GreyboxSceneBuilder.Build()` (menú *TrashPandas > Build Greybox Scene*), idempotente: regenera la escena y el prefab.

- [ ] **Step 1: Escribir el builder**

`TrashPandas/Assets/Scripts/Editor/GreyboxSceneBuilder.cs`:
```csharp
using System.IO;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Raccoon;
using TrashPandas.Runtime.Trenchcoat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrashPandas.EditorTools
{
    /// <summary>Builds the greybox test scene deterministically. Safe to re-run.</summary>
    public static class GreyboxSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Greybox_Trenchcoat.unity";
        const string PrefabPath = "Assets/Prefabs/Raccoon.prefab";
        const string MaterialDir = "Assets/Materials/Greybox";

        [MenuItem("TrashPandas/Build Greybox Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory(MaterialDir);

            var grass = Mat("Grass", new Color(0.45f, 0.65f, 0.35f));
            var wood = Mat("Wood", new Color(0.85f, 0.8f, 0.7f));
            var coat = Mat("Coat", new Color(0.55f, 0.42f, 0.3f));
            var skin = Mat("Skin", new Color(0.95f, 0.8f, 0.7f));
            var fur = Mat("Fur", new Color(0.45f, 0.45f, 0.5f));
            var curtain = Mat("Curtain", new Color(0.8f, 0.3f, 0.35f));
            var hedge = Mat("Hedge", new Color(0.2f, 0.45f, 0.2f));

            var raccoonPrefab = BuildRaccoonPrefab(fur);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            light.intensity = 1.2f;

            var ground = Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), grass);

            // Tables: the coat can't fit under them; a crouched raccoon can (gap 0.75 m, raccoon crouch 0.3 m).
            for (int i = 0; i < 6; i++)
            {
                var pos = new Vector3(-6f + (i % 3) * 6f, 0f, 4f + (i / 3) * 5f);
                var table = new GameObject($"Table_{i}");
                table.transform.position = pos;
                Box("Top", pos + new Vector3(0f, 0.8f, 0f), new Vector3(2f, 0.1f, 1.2f), wood).transform.SetParent(table.transform, true);
                foreach (var leg in new[] { new Vector3(-0.9f, 0f, -0.5f), new Vector3(0.9f, 0f, -0.5f), new Vector3(-0.9f, 0f, 0.5f), new Vector3(0.9f, 0f, 0.5f) })
                    Box("Leg", pos + leg + new Vector3(0f, 0.375f, 0f), new Vector3(0.08f, 0.75f, 0.08f), wood).transform.SetParent(table.transform, true);
            }

            // Climbable curtain with a ledge on top.
            var curtainGo = Box("Curtain_Climbable", new Vector3(10f, 2f, 0f), new Vector3(2f, 4f, 0.1f), curtain);
            curtainGo.AddComponent<Climbable>();
            Box("Ledge", new Vector3(10f, 4.05f, 0.6f), new Vector3(2f, 0.1f, 1.3f), wood);

            // Hedge wall with a raccoon-sized gap (1 m wide, 0.5 m tall: only a crouched raccoon fits).
            Box("Hedge_Left", new Vector3(-4.25f, 1f, -8f), new Vector3(7.5f, 2f, 1f), hedge);
            Box("Hedge_Right", new Vector3(4.25f, 1f, -8f), new Vector3(7.5f, 2f, 1f), hedge);
            Box("Hedge_Top", new Vector3(0f, 1.25f, -8f), new Vector3(1f, 1.5f, 1f), hedge);
            var hedgeClimb = Box("Hedge_Climbable", new Vector3(8f, 1f, -8f), new Vector3(1f, 2f, 1f), hedge);
            hedgeClimb.AddComponent<Climbable>();

            var body = BuildTrenchcoat(coat, skin);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 3f, -5f);
            var follow = camGo.AddComponent<FollowCamera>();

            var controller = new GameObject("TrenchcoatController").AddComponent<TrenchcoatController>();
            controller.PlayerCount = 3;
            controller.Body = body;
            controller.RaccoonPrefab = raccoonPrefab;
            controller.Camera = follow;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[GreyboxSceneBuilder] Built {ScenePath}");
        }

        static TrenchcoatBody BuildTrenchcoat(Material coat, Material skin)
        {
            var root = new GameObject("Trenchcoat");
            root.transform.position = new Vector3(0f, 0.05f, 0f);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 60f;
            var col = root.AddComponent<CapsuleCollider>();
            col.height = 1.9f;
            col.radius = 0.35f;
            col.center = new Vector3(0f, 0.95f, 0f);

            var torso = Visual(PrimitiveType.Capsule, "Torso", root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.5f), coat);
            var head = Visual(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.75f, 0f), Vector3.one * 0.35f, skin);
            Visual(PrimitiveType.Cube, "Hat", head, new Vector3(0f, 0.55f, 0f), new Vector3(1.1f, 0.4f, 1.1f), coat);
            var left = Visual(PrimitiveType.Sphere, "LeftHand", root.transform, new Vector3(-0.4f, 0.6f, 0f), Vector3.one * 0.18f, skin);
            var right = Visual(PrimitiveType.Sphere, "RightHand", root.transform, new Vector3(0.4f, 0.6f, 0f), Vector3.one * 0.18f, skin);

            var body = root.AddComponent<TrenchcoatBody>();
            body.Torso = torso;
            body.Head = head;
            body.LeftHand = left;
            body.RightHand = right;
            return body;
        }

        static RaccoonController BuildRaccoonPrefab(Material fur)
        {
            var root = new GameObject("Raccoon");
            var cc = root.AddComponent<CharacterController>();
            cc.height = 0.6f;
            cc.radius = 0.2f;
            cc.center = new Vector3(0f, 0.3f, 0f);
            Visual(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.4f, 0.3f, 0.4f), fur);
            Visual(PrimitiveType.Sphere, "Snout", root.transform, new Vector3(0f, 0.4f, 0.22f), Vector3.one * 0.12f, fur);
            root.AddComponent<RaccoonController>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<RaccoonController>();
        }

        static GameObject Box(string name, Vector3 position, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Transform Visual(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        static Material Mat(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
```

- [ ] **Step 2: Generar la escena** (editor cerrado)

Run: `tools/unity-run.sh TrashPandas.EditorTools.GreyboxSceneBuilder.Build && grep -c "Built Assets/Scenes/Greybox_Trenchcoat.unity" .test-results/run.log`
Expected: exit 0 e imprime `1`. Existen `TrashPandas/Assets/Scenes/Greybox_Trenchcoat.unity` y `TrashPandas/Assets/Prefabs/Raccoon.prefab`.

- [ ] **Step 3: Verificar que la suite sigue verde**

Run: `tools/unity-test.sh`
Expected: `total="42" passed="42" failed="0"`

- [ ] **Step 4 (con el usuario): Prueba de juego manual**

Abrir el proyecto en Unity Hub → abrir `Assets/Scenes/Greybox_Trenchcoat.unity` → Play. Marcar cada punto:

- [ ] El HUD muestra 3 puestos (Legs, Arms, Head) y `◀ YOU` en el slot 0.
- [ ] Con P0 (piernas), W avanza y A/D gira. El señor se bambolea al caminar.
- [ ] Espacio con P0 hace saltar al señor (un solo puesto controla las dos piernas, así que el salto siempre está coordinado).
- [ ] Tab → P1 (brazos): el mouse mueve las dos manos y los clics izquierdo/derecho "aprietan" cada mano.
- [ ] Tab → P2 (cabeza): las flechas giran la cabeza.
- [ ] Con P1 activo, E: sale un mapache, el HUD marca `Arms` en Missing, **las manos cuelgan** y la cámara sigue al mapache.
- [ ] El mapache corre (WASD), salta (Espacio), **trepa** la cortina roja y el arbusto marcado (caminando contra ellos), **se mete agachado (Ctrl) debajo de una mesa** y **pasa agachado por el hueco del seto**.
- [ ] Lejos del señor, E muestra "Too far from the coat". Cerca, E lo regresa, las manos vuelven a funcionar y la cámara vuelve al señor.
- [ ] Con P0 afuera (piernas vacías), **el señor colapsa** y no se mueve. Con P2 afuera, **la cabeza se ladea**.
- [ ] Cambiar `PlayerCount` a 4 en el inspector de `TrenchcoatController` → Play: con piernas separadas, avanzar con solo una pierna hace que el señor **gire en círculos**, y Espacio con una sola pierna **no salta**.

- [ ] **Step 5 (con el usuario): La prueba del clip** 🎬

Grabar 3–5 minutos jugando (Cmd+Shift+5 en macOS) con un amigo mirando o turnándose el teclado. Pregunta de la etapa: **¿controlar al señor y que se descomponga ya da risa?** Anotar en `docs/playtests/2026-xx-xx-greybox.md` qué dio risa, qué frustró y qué ajustar (velocidades, bamboleo, ventana de salto). Si no da risa, se ajustan los parámetros de `TrenchcoatBody` y `MixerSettings` antes de pasar a la etapa 2.

- [ ] **Step 6: Commit**

```bash
git add TrashPandas/Assets/Scripts/Editor TrashPandas/Assets/Scenes TrashPandas/Assets/Prefabs TrashPandas/Assets/Materials TrashPandas/ProjectSettings/EditorBuildSettings.asset docs/playtests
git commit -m "feat: add generated greybox scene for trenchcoat playtesting

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Fuera de este plan (siguientes planes)

Etapa 2 (online con Steam), 3 (boda gris: NPCs, gato, mesero, sospecha, diálogos, botín), 4 (¡RUN!), 5 (guarida), 6 (arte), 7 (playtest y Steam). Cada una tendrá su propio plan, escrito con lo aprendido en la prueba del clip de esta etapa.
