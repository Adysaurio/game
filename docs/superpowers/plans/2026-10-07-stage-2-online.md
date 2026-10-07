# Etapa 2: online (host + Relay) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** De 2 a 5 jugadores juegan la escena gris por internet. Uno hospeda y comparte un código; los demás lo escriben y entran. Cada jugador controla su puesto con su propia cámara, puede salir como mapache y volver, y los objetos agarrados se ven igual para todos. El modo debug de una sola persona sigue funcionando sin red.

**Architecture:** Host-autoritativo (spec §9). El host simula la gabardina, los puestos, el agarre y los objetos. Cada cliente calcula su input en espacio mundo con su propia cámara (`SlotInput`, que ya es independiente de la cámara) y lo envía por RPC no confiable a ~30 Hz. El host aplica `SlotInputRouter` → `TrenchcoatIntentMixer` → `TrenchcoatBody` y replica la pose con `NetworkTransform` (interpolado) más un `BodyVisualState` compacto, para que cada cliente anime piernas, brazos y cabeza localmente. Los mapaches sueltos son `NetworkObject` con autoridad del dueño, para que se sientan inmediatos. La conexión está detrás de `ISessionConnector`: `LocalConnector` (IP directa, para Multiplayer Play Mode) y `RelayConnector` (Unity Multiplayer Services con código). Más adelante se agrega `SteamConnector` sin tocar el juego.

**Tech Stack:** Unity 6.6, Netcode for GameObjects 2.13.3, Unity Transport 6.6.0, Multiplayer Services 2.3.3 (Sessions + Relay), Multiplayer Play Mode 3.0.0, Cinemachine 6.6, NUnit.

**Spec:** `docs/superpowers/specs/2026-10-07-trash-pandas-trenchcoat-design.md` §9 (Red), §10 (casos borde), §11. **Previo:** `docs/superpowers/plans/2026-10-07-stage-1b-controls-rework.md`.

## Global Constraints

- De 2 a 5 jugadores; los puestos se reparten con `SlotLayout` al **iniciar la ronda** con el número de jugadores conectados. No hay entrada tardía a media ronda.
- El host es la autoridad de la gabardina, los puestos, el agarre y la sospecha; los clientes nunca mueven la gabardina directamente.
- Desconexión de un cliente: su puesto queda vacío (`SlotSystem.Leave`) y lo que cargaba se suelta. Desconexión del host: todos vuelven al menú con un aviso. Sin migración de host.
- `TrashPandas.Core` no depende de Netcode. La serialización vive en Runtime.
- Costo de operación $0: sin servidores dedicados; Relay dentro del nivel gratuito.
- El modo debug de una persona (sin red) debe seguir funcionando igual que hoy.
- Verificación en vivo con Unity Pipeline y Multiplayer Play Mode antes de pasarle cualquier cosa al usuario.

## Review Focus

1. **Un cliente se desconecta mientras carga algo:** su puesto queda vacío y el objeto cae; no queda flotando. → `Roster_Disconnect_FreesSlot` (Core) + verificación en MPPM.
2. **Dos jugadores entran al mismo tiempo al último puesto libre al volver a la gabardina:** solo uno entra. → `SlotSystem` ya lo garantiza; test `Roster_ReturnRace_OnlyOneEnters`.
3. **Input viejo o duplicado (paquetes no confiables desordenados):** el host ignora inputs con número de secuencia menor. → `InputSequencer_DropsStaleInputs`.
4. **El host cierra el juego a media ronda:** los clientes ven "El anfitrión se desconectó" y vuelven al menú, sin quedarse congelados.
5. **Código de sala mal escrito o vencido:** mensaje claro ("No encontramos esa sala") sin cerrar el juego.

## Tasks

### Task 1: Paquetes y estructura
- Instalar `com.unity.netcode.gameobjects@2.13.3`, `com.unity.multiplayer.playmode@3.0.0` y `com.unity.services.multiplayer@2.3.3` (Unity Transport 6.6.0 viene como dependencia).
- Nuevas referencias: `TrashPandas.Runtime.asmdef` → `Unity.Netcode.Runtime`, `Unity.Networking.Transport`, `Unity.Services.Core`, `Unity.Services.Multiplayer`. Nueva asmdef de tests `TrashPandas.Tests.EditMode.Net` (referencia Runtime + Netcode) para las pruebas de serialización.
- Verificación: recompilar sin errores; 65/65 tests siguen pasando.

### Task 2: Core: `SessionRoster` e `InputSequencer` (TDD)
Files: `Core/Session/SessionRoster.cs`, `Core/Session/InputSequencer.cs`, tests.
- `SessionRoster`: `Join(ulong clientId) → bool` (máx. 5; falla si la ronda ya empezó), `Leave(ulong)`, `StartRound() → SlotSystem` (falla con menos de 2), asignación inicial de puestos en orden de llegada, `PlayerIdOf(ulong)`, y en ronda `Disconnect(ulong)` → `SlotSystem.Leave`.
- `InputSequencer`: por jugador guarda el último `seq` y `Accept(playerId, seq) → bool`.
- Tests: `Roster_JoinUpToFive`, `Roster_RejectsJoinDuringRound`, `Roster_StartNeedsTwo`, `Roster_AssignsSlotsInJoinOrder`, `Roster_Disconnect_FreesSlot`, `Roster_ReturnRace_OnlyOneEnters`, `InputSequencer_DropsStaleInputs`, `InputSequencer_TracksPlayersIndependently`.

### Task 3: Serialización de red (TDD)
Files: `Runtime/Net/NetSlotInput.cs` (`INetworkSerializable` envolviendo `SlotInput` + `seq`), `Runtime/Net/BodyVisualState.cs` (flags de puestos y partes, `Move`, `Discord`, puntos de alcance y apuntado de cabeza, cuantizados a `half`), tests de ida y vuelta con `FastBufferWriter`/`FastBufferReader`, incluyendo `NegativeInfinity` en `JumpPressedAt`.

### Task 4: Conexión: `ISessionConnector`, `LocalConnector`, `RelayConnector`
Files: `Runtime/Net/ISessionConnector.cs`, `LocalConnector.cs` (UnityTransport a 127.0.0.1:7777), `RelayConnector.cs` (Multiplayer Services: crear sesión con Relay → `Code`; unirse por código), `NetworkBootstrap.cs` (`NetworkManager` + `UnityTransport` + registro de prefabs).
- Errores amigables: código inválido, sin internet o servicios sin configurar ("Conecta el proyecto a Unity Cloud").
- El host recibe `OnClientDisconnectCallback` → roster; los clientes reciben `OnTransportFailure`/desconexión del servidor → menú con aviso.

### Task 5: Gabardina en red
Files: `Runtime/Net/NetworkedTrenchcoat.cs`, cambios en `TrenchcoatBody` (modo "solo visual" en clientes: Rigidbody cinemático, visuales desde `BodyVisualState`).
- Cliente: cada frame arma su `SlotInput` con su `PlayerCameraRig` y lo envía por `SubmitInputRpc` (no confiable, 30 Hz, con `seq`).
- Host: guarda el último input aceptado por jugador → Router → Mixer → Body; publica `BodyVisualState` (NetworkVariable, 20 Hz) y la pose por `NetworkTransform` interpolado.
- `TrenchcoatController` se divide: `OfflineDebugController` (modo actual de una persona) y `OnlinePlayerController` (cámara local, input, HUD de tu puesto).

### Task 6: Mapaches en red
- Salir (`E`): RPC al host → `SlotSystem.Leave` → `Spawn` del mapache con dueño = ese cliente; el cliente cambia su cámara al mapache.
- Volver (`E` cerca): RPC → el host valida distancia y puesto libre → `Despawn` → la cámara vuelve a la gabardina.
- Movimiento con autoridad del dueño (`NetworkTransform` en modo owner) para respuesta inmediata.

### Task 7: Agarre en red
- Grabbables con `NetworkObject` + `NetworkTransform` (servidor) + `NetworkRigidbody`.
- `HandGrabber` corre solo en el host. El "seguir la mano" se replica por el transform del objeto; en los clientes el objeto se interpola.
- Desconexión con objeto en mano → se suelta (Review Focus 1).

### Task 8: Menú y sala
- Escena `Menu` (generada por el builder): **Hospedar** (muestra el código para copiar), **Unirse** (campo de código), **Probar local** (para MPPM), **Modo debug (1 jugador)**.
- Sala: lista de jugadores conectados y botón **Iniciar** (solo el host, con 2 o más jugadores) → todos cargan `Greybox_Trenchcoat`.

### Task 9: Verificación
1. **MPPM local:** host + 1 y 2 jugadores virtuales: caminar, girar, agarrar, salir y volver, desconectar a un jugador y cerrar el host. Medir con Pipeline (posiciones y estado en cada instancia) y capturas.
2. **Relay:** el usuario conecta el proyecto a Unity Cloud (paso guiado). Prueba host + jugador virtual vía código.
3. **Con un amigo:** build de Windows y de macOS; el usuario la manda en zip. Prueba de 10 minutos con la prueba del clip.
