# Space Shooter LAN

Arcade 2D tipo Galaga para 1 a 4 jugadores, individual o cooperativo en una red local. El proyecto usa un servidor autoritativo independiente y clientes gráficos desarrollados con Godot y C#.

El desarrollo está organizado por fases con verificaciones automatizadas y validaciones manuales de la experiencia multijugador.

## Descripción

Space Shooter LAN es una solución de software distribuido basada en el modelo cliente-servidor. Implementa un juego arcade bidimensional inspirado en juegos de naves verticales, diseñado para que entre uno y cuatro jugadores participen desde equipos conectados a una misma red local.

El servidor es el componente autoritativo: mantiene el estado de la partida, procesa la simulación, valida las acciones recibidas y distribuye a los clientes periódicamente los estados públicos del mundo. La arquitectura evita que un cliente pueda modificar directamente las posiciones, los disparos, las colisiones o la puntuación de los jugadores.

El proyecto se divide en tres componentes principales:

- **Cliente:** utiliza Godot 4.6 con C# para administrar las pantallas, el lobby, la entrada del usuario, la reproducción visual y la representación de los estados recibidos. Incluye interpolación de posiciones, indicadores de estado y un HUD con información de la partida.
- **Servidor:** es una aplicación de consola desarrollada en C# que administra sesiones, conexiones TCP, lobby, campaña, partidas, colisiones y persistencia de récords.
- **Código compartido:** define los tipos de dominio y los contratos de protocolo utilizados por ambos extremos, evitando que dependan de implementaciones específicas de Godot o del servidor.

La comunicación utiliza TCP/IPv4 por el puerto `7777`. Los clientes envían su nombre, la cantidad solicitada de jugadores, la nave seleccionada y las entradas de movimiento y disparo; el servidor responde con los mensajes de lobby y los snapshots del mundo. La simulación se ejecuta a 60 Hz y los clientes reciben información para representar el juego a 20 Hz. Los snapshots se interpolan para suavizar el movimiento visual y no reemplazar el estado autoritativo guardado por el servidor.

La partida admite hasta cuatro jugadores y puede realizarse en modo individual o cooperativo. El servidor administra hasta diez fases con enemigos `Scout`, `Diver` y `Tank`, además de un jefe con diferentes patrones de ataque. Cada fase incluye una presentación sincronizada, enemigos con estados y comportamiento propio, power-ups, disparos, colisiones y puntuación. Cuando finaliza una partida, el servidor registra los mejores resultados en el archivo `records.json` y permite solicitar una revancha sin cerrar la conexión.

Las principales tecnologías y herramientas utilizadas son:

- C# y .NET 10 para el servidor, el dominio compartido y las pruebas.
- Godot 4.6 .NET para el cliente gráfico.
- TCP e IPv4 para la comunicación en red local.
- JSON para almacenar los récords.
- xUnit para las pruebas automatizadas del código compartido y del servidor.
- Rider o la CLI de .NET para compilar, ejecutar y probar la solución.
- HTML y JavaScript para los diagramas interactivos de arquitectura y secuencia.

Como requisito operativo, el servidor y los clientes deben ejecutarse en una red local o en un equipo local mediante `127.0.0.1`. Los clientes deben utilizar el puerto `7777` y deben seleccionar el mismo tamaño de partida para que el lobby complete el cupo correspondiente. El proyecto está diseñado para Windows 10 u 11 de 64 bits.

Las restricciones actuales del sistema son las siguientes:

- El acceso directo desde Internet no está habilitado; la comunicación está pensada para LAN o para pruebas locales.
- La representación gráfica utiliza recursos geométricos y texturas de demostración, no un conjunto final de arte personalizado.
- La partida requiere que todos los participantes puedan alcanzar el servidor y que el firewall permita el puerto TCP `7777`.
- La solución requiere el SDK de .NET 10.0.401 o una versión 10.0 posterior compatible y Godot 4.6 .NET.
- La versión actual ofrece lobby, selección de nave, campaña, power-ups, finalización y revancha, pero no implementa servicios externos, cuentas de usuario ni partidas mediante Internet.

## Conclusiones

La implementación permitió aplicar conceptos de programación concurrente en un escenario real: conexiones simultáneas, sesiones independientes, cola de mensajes, simulación periódica, distribución de snapshots y persistencia sin perder la autoría del servidor. La separación entre cliente, servidor y contratos compartidos facilitó mantener cada responsabilidad en un componente distinto y permitió probar partes del sistema de forma independiente.

El trabajo también mostró la importancia de definir un protocolo claro. Los mensajes compartidos, la serialización y los tipos de identificación permiten que la comunicación sea verificable y que el cliente sólo represente estados que provienen del servidor. La interpolación mejora la presentación visual, aunque el estado confirmado continúa siendo el resultado de la simulación autoritativa.

La mayor dificultad fue coordinar el ciclo completo entre lobby, conexión, ejecución simultánea y desconexión sin duplicar o perder estados. También requirió prestar especial atención a los recursos compartidos, el cierre limpio de sockets, la cancelación de tareas y la actualización segura de los registros de puntuación.

Las pruebas automatizadas resultaron fundamentales para validar el dominio compartido y la lógica del servidor, mientras que las pruebas manuales permitieron verificar el comportamiento real de varios clientes en la misma computadora y desde equipos de una LAN. Esta combinación permitió detectar problemas que no aparecen al ejecutar únicamente una instancia aislada.

Como mejoras futuras podrían agregarse arte definitivo, efectos visuales adicionales, mayor variedad de niveles, autenticación de usuarios, escalabilidad a partidas fuera de la LAN y herramientas más completas de diagnóstico de latencia y desconexiones. Incluso con estas mejoras pendientes, el proyecto integra de forma funcional los procesos de red, concurrencia, simulación, persistencia y representación gráfica definidos para el trabajo integrador.

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

La campaña incluye diez fases con `Scout`, `Diver` y `Tank`, y culmina con un jefe de dos formas de ataque. Antes de cada fase, el servidor pausa el combate durante tres segundos y sincroniza en todos los clientes una presentación con el número de fase, la bonificación obtenida y la cuenta regresiva. Los enemigos derrotados generan distintos power-ups. Al terminar, el servidor actualiza atómicamente `records.json` en la carpeta desde la que fue ejecutado.

## Ejecutar el cliente

Abrir `Client/project.godot` y presionar `F5`. La pantalla de conexión solicita la IP del servidor, el nombre del jugador y un tamaño de partida entre `1 player` y `4 players`. En desarrollo local se utiliza `127.0.0.1`; desde otra computadora de la LAN se utiliza la IPv4 del equipo que ejecuta el servidor. Todos los clientes deben elegir el mismo tamaño; el primero fija el cupo del lobby.

En el lobby, seleccionar una nave y marcar `Ready`. La partida comienza cuando se completa exactamente el cupo elegido y todos están listos. Los controles del vertical slice son:

- Flechas: movimiento.
- Espacio: disparo.

Al terminar, cada cliente puede aceptar una revancha sin cerrar la conexión o volver a la pantalla inicial. Una revancha comienza cuando se vuelve a completar el cupo y todos los jugadores conectados están listos. Volver al inicio libera el lugar de ese jugador y permite elegir nuevamente una partida de uno a cuatro participantes.

Los gráficos actuales son placeholders geométricos dibujados por código. Cada cliente interpola los snapshots recibidos y sólo representa el estado confirmado por el servidor.

No utilizar la configuración `Player GDScript` de Rider: el proyecto usa C#.

## Diagramas interactivos del diseño final

- [`Arquitectura objetivo`](docs/diagrams/final-architecture.html): procesos, autoridad, colas, simulación, paralelismo y persistencia.
- [`Conexión, lobby e inicio`](docs/diagrams/final-lobby.html): handshake, selección, estado listo y comienzo sincronizado.
- [`Entrada, simulación y snapshots`](docs/diagrams/final-game-loop.html): validación, game loop, actualización de enemigos y representación.
- [`Fin de partida y cierre`](docs/diagrams/final-match-end-shutdown.html): ranking, récords, cancelación y liberación de recursos.
