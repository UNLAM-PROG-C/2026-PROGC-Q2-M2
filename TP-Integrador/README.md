# Space Shooter LAN

Arcade 2D tipo Galaga para 1 a 4 jugadores, individual o cooperativo en una red local. El proyecto usa un servidor autoritativo independiente y clientes gráficos desarrollados con Godot y C#.

El desarrollo está organizado por fases con verificaciones automatizadas y validaciones manuales de la experiencia multijugador.

## Requisitos

- Windows 10 u 11 de 64 bits.
- .NET SDK 10.0.401 o un parche 10.0 posterior compatible.
- Godot 4.6 .NET, no la edición Standard.
- JetBrains Rider recomendado; también es posible compilar con la CLI de .NET.

Para verificar el SDK instalado:

```powershell
dotnet --info
```

## Clonar el proyecto

Clonar el repositorio y entrar en la carpeta del trabajo integrador:

```powershell
git clone https://github.com/UNLAM-PROG-C/2026-PROGC-Q2-M2.git
cd 2026-PROGC-Q2-M2\TP-Integrador
```

Los comandos restantes de este README se ejecutan desde `TP-Integrador`.

## Estructura

```text
Client/                 Cliente Godot .NET
Server/                 Servidor de consola autoritativo
Shared/                 Contratos compartidos sin dependencia de Godot
Tests/Shared.Tests/     Pruebas del código compartido
Tests/Server.Tests/     Pruebas del servidor
docs/diagrams/          Diagramas interactivos de arquitectura y secuencia
SpaceShooterLAN.sln     Solución principal para Rider y dotnet
```

## Abrir el proyecto

En Rider, abrir la solución raíz:

```text
SpaceShooterLAN.sln
```

En Godot, importar o abrir:

```text
Client/project.godot
```

Godot y Rider pueden permanecer abiertos simultáneamente. Godot administra escenas, recursos y ejecución visual; Rider administra código, pruebas y el servidor.

## Restaurar, compilar y probar

Desde la carpeta raíz:

```powershell
dotnet restore SpaceShooterLAN.sln
dotnet build SpaceShooterLAN.sln --no-restore
dotnet test SpaceShooterLAN.sln --no-build
```

La primera restauración requiere acceso a NuGet.

## Ejecutar el servidor

```powershell
dotnet run --project Server/SpaceShooter.Server.csproj
```

El servidor escucha conexiones TCP/IPv4 en el puerto `7777` y permanece activo hasta recibir `Ctrl+C`. Mantiene hasta cuatro sesiones persistentes, gestiona el lobby y ejecuta la simulación autoritativa a 60 Hz. Durante la partida distribuye snapshots a 20 Hz y resuelve movimiento, disparos, colisiones y puntuación.

Para jugar desde otras computadoras de la LAN, Windows debe permitir al servidor comunicarse en redes privadas. Si aparece el aviso del firewall al ejecutarlo por primera vez, habilitar el acceso para redes privadas; si no aparece, crear manualmente una regla de entrada para el puerto TCP `7777`. No es necesario abrir el puerto para probar varios clientes con `127.0.0.1` en la misma computadora.

La partida incluye tres oleadas con `Scout`, `Diver` y `Tank`, seguidas por un jefe de dos fases. Los enemigos derrotados generan power-ups de disparo rápido, disparo múltiple y escudo. Al terminar, el servidor actualiza atómicamente `records.json` en la carpeta desde la que fue ejecutado.

## Ejecutar el cliente

Abrir `Client/project.godot` y presionar `F5`. La pantalla de conexión solicita la IP del servidor, el nombre del jugador y un tamaño de partida entre `1 player` y `4 players`. En desarrollo local se utiliza `127.0.0.1`; desde otra computadora de la LAN se utiliza la IPv4 del equipo que ejecuta el servidor. Todos los clientes deben elegir el mismo tamaño; el primero fija el cupo del lobby.

En el lobby, seleccionar una nave y marcar `Ready`. La partida comienza cuando se completa exactamente el cupo elegido y todos están listos. Los controles del vertical slice son:

- Flechas: movimiento.
- Espacio: disparo.

Los gráficos actuales son placeholders geométricos dibujados por código. Cada cliente interpola los snapshots recibidos y sólo representa el estado confirmado por el servidor.

No utilizar la configuración `Player GDScript` de Rider: el proyecto usa C#.

## Diagramas interactivos del diseño final

- [`Arquitectura objetivo`](docs/diagrams/final-architecture.html): procesos, autoridad, colas, simulación, paralelismo y persistencia.
- [`Conexión, lobby e inicio`](docs/diagrams/final-lobby.html): handshake, selección, estado listo y comienzo sincronizado.
- [`Entrada, simulación y snapshots`](docs/diagrams/final-game-loop.html): validación, game loop, actualización de enemigos y representación.
- [`Fin de partida y cierre`](docs/diagrams/final-match-end-shutdown.html): ranking, récords, cancelación y liberación de recursos.

Los diagramas representan el objetivo de la Fase 9. Distinguen mediante sus notas lo implementado hasta la Fase 5 de las capacidades todavía planificadas para las Fases 6 a 9.

## Estado actual

Fases 0 a 4 completadas y aprobadas. La Fase 5 está implementada y aprobó sus verificaciones automatizadas; queda pendiente la validación manual del juego completo en Godot.
