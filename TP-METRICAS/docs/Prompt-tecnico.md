# Proyecto: Rate Limiter concurrente para servidor web

> Documento de contexto para una herramienta de IA. Contiene el problema, los requisitos, las decisiones de diseño recomendadas, los criterios de aceptación y el plan de trabajo. Leelo completo antes de escribir código.

---

## 1. Resumen del problema

Un servidor web necesita limitar la cantidad de peticiones que recibe de cada usuario en una ventana de tiempo.

**Ejemplo base:** cada usuario puede hacer como máximo **5 peticiones en 10 segundos**. La sexta petición dentro de ese intervalo se rechaza.

El rate limiter guarda, por cada usuario, el historial o contador de peticiones dentro de la ventana. El usuario se identifica con cualquier clave única: id de usuario, IP, fingerprint, API key, etc.

## 2. El desafío central: condición de carrera

El servidor atiende cada conexión en un **hilo distinto**. Supongamos que un usuario ya hizo 4 peticiones y llegan dos más al mismo tiempo:

1. El hilo A lee el historial del usuario: 4 peticiones, todavía hay lugar.
2. El hilo B lee el historial del usuario: 4 peticiones, todavía hay lugar.
3. Ambos dejan pasar su petición y registran una más.
4. Resultado: el usuario hizo **6 peticiones**, el límite se excedió.

Este es un clásico **check-then-act** (TOCTOU): la verificación y la actualización no son atómicas. El resultado depende del orden en que el planificador ejecute los hilos, así que el bug es intermitente y difícil de reproducir.

### La solución ingenua que NO queremos

Poner un lock global sobre todo el hashmap resuelve la carrera, pero convierte el servidor en un modelo **secuencial**: todos los usuarios esperan al mismo lock y la performance se degrada.

### Objetivo del proyecto

Resolver la condición de carrera **sin serializar a usuarios distintos**. Dos peticiones de usuarios diferentes deben poder evaluarse en paralelo. Solo las peticiones del *mismo* usuario pueden competir entre sí, y la sección crítica debe ser mínima.

## 3. Requisitos funcionales

- **RF1.** `allow(key)` devuelve `true` si la petición está permitida y `false` si debe rechazarse.
- **RF2.** Semántica exacta: en cualquier intervalo de longitud `window`, un mismo `key` tiene como máximo `limit` peticiones **aceptadas**.
- **RF3.** Las peticiones rechazadas **no consumen cupo** (no se registran en el historial). Documentar esta decisión en el README.
- **RF4.** `limit` y `window` son configurables (por defecto 5 y 10 s). Opcional: configuración distinta por clave o por tipo de cliente.
- **RF5.** Claves independientes: el estado de un usuario nunca afecta a otro.
- **RF6.** Resolución de la clave de usuario mediante una estrategia intercambiable (IP, header, id autenticado, etc.).
- **RF7.** Al rechazar, el servidor responde **HTTP 429** con el header `Retry-After` (segundos hasta que se libere cupo). Opcional: `X-RateLimit-Limit`, `X-RateLimit-Remaining`.
- **RF8.** Limpieza de usuarios inactivos para que la memoria no crezca sin límite (ver sección 6).

## 4. Requisitos no funcionales

- **RNF1. Corrección bajo concurrencia:** el límite nunca se excede, sin importar el orden de ejecución de los hilos. No se aceptan soluciones "que casi siempre funcionan".
- **RNF2. Escalabilidad:** el throughput debe crecer al aumentar hilos cuando las claves son distintas. Debe demostrarse con un benchmark.
- **RNF3. Memoria acotada:** como máximo `limit` timestamps por usuario activo, y eliminación de usuarios inactivos.
- **RNF4. Testeabilidad:** el reloj debe ser inyectable para poder probar la expiración de la ventana sin hacer `sleep`.
- **RNF5. Simplicidad:** preferir la solución más simple que cumpla RNF1 y RNF2. No sobre-ingeniar.

## 5. Diseño recomendado

### 5.1 Interfaz

```text
interface RateLimiter:
    allow(key) -> Decision

Decision:
    allowed: bool
    retryAfter: duration      # 0 si allowed
    remaining: int            # cupo restante en la ventana
```

La implementación concreta es intercambiable (ver 5.3). El servidor depende solo de la interfaz.

### 5.2 Algoritmo de referencia: ventana deslizante con log (sliding window log)

Es el que mejor coincide con el enunciado ("historial de peticiones dentro de una ventana") y es **exacto**.

Estado por usuario: una cola de timestamps de peticiones aceptadas, ordenada.

```text
allow(key):
    now = clock.monotonicNow()
    ejecutar ATÓMICAMENTE para este key:
        descartar del frente de la cola los timestamps <= now - window
        si cola.size < limit:
            cola.add(now)
            return Decision(allowed=true, remaining = limit - cola.size)
        sino:
            retryAfter = (cola.peekFirst() + window) - now
            return Decision(allowed=false, retryAfter=retryAfter)
```

Complejidad: O(1) amortizado por petición, memoria O(`limit`) por usuario.

### 5.3 Cómo hacer atómico el bloque crítico SIN un lock global

Implementar al menos las dos primeras y comparar con la ingenua en el benchmark:

| Variante | Idea | Notas |
|---|---|---|
| **A. Operación atómica por clave** | Usar la operación atómica del mapa concurrente (`ConcurrentHashMap.compute`, `sync.Map` + mutex por clave, `DashMap::entry`, etc.) para ejecutar el bloque crítico. | Lo más simple. Solo se serializan peticiones del mismo usuario (o de claves en el mismo bucket interno). La función pasada al `compute` debe ser corta y no tocar el mapa. |
| **B. Lock por usuario (lock striping)** | Mapa concurrente de `key -> Bucket`, cada `Bucket` con su propio mutex. Alternativa: array fijo de N locks y `hash(key) % N`. | Control más explícito. Ojo con la limpieza (ver 6). |
| **C. Sin locks (CAS)** | Estado inmutable por usuario en una referencia atómica; leer, calcular nuevo estado, `compareAndSet`, reintentar si falló. | Más complejo; útil con contadores o token bucket. Para el log completo suele ser excesivo. |
| **Ingenua (solo comparativa)** | Un único lock global sobre todo el mapa. | Sirve como línea base del benchmark y para mostrar el costo de serializar. |

Además, incluir una **versión deliberadamente rota** (leer y luego escribir sin atomicidad) para demostrar el bug con un test que falla. Es el punto de partida del proyecto.

### 5.4 Algoritmos alternativos (opcionales, tras la implementación principal)

- **Ventana fija (fixed window counter):** un contador por ventana. Muy barato, pero permite ráfagas de hasta 2× el límite en el borde entre dos ventanas.
- **Ventana deslizante con contador (aproximada):** combina dos ventanas fijas con ponderación. Memoria O(1) por usuario, con un pequeño error.
- **Token bucket:** permite ráfagas controladas, estado O(1), se actualiza fácil con CAS o un lock por usuario.

Si se implementan, deben respetar la misma interfaz y pasar la misma batería de tests de corrección (ajustando la semántica documentada).

## 6. Puntos delicados que la implementación debe resolver

1. **Reloj monotónico.** Usar un reloj monotónico (`System.nanoTime`, `time.monotonic`, `Instant` de proceso, etc.) y **no** el reloj de pared, que puede saltar hacia atrás o adelante por ajustes NTP.
2. **Carrera entre limpieza y uso.** Si un hilo de limpieza elimina un `Bucket` del mapa mientras otro hilo todavía tiene una referencia a él, esa petición se contaría en un objeto huérfano y el usuario podría exceder el límite. Soluciones válidas:
   - Hacer la limpieza dentro de la misma operación atómica por clave (`computeIfPresent` que devuelve `null` si el bucket quedó vacío y expirado).
   - O marcar el bucket como "retirado" bajo su lock y hacer que quien lo encuentre así reintente con un bucket nuevo.
3. **Limpieza de inactivos.** Tarea periódica (o limpieza oportunista) que borre usuarios sin peticiones en la última ventana. Sin esto, un atacante con muchas IPs distintas agota la memoria.
4. **Orden de verificación y registro.** Verificar y registrar deben ocurrir en el **mismo** bloque atómico. Nunca leer fuera del bloque y escribir dentro.
5. **Identificación del cliente.**
   - La IP detrás de un proxy o balanceador llega en `X-Forwarded-For`, que **el cliente puede falsificar**. Solo confiar en ese header si el proxy es de confianza.
   - Considerar agrupar IPv6 por prefijo (por ejemplo /64), porque un solo usuario puede tener miles de direcciones.
   - Varios usuarios legítimos pueden compartir una IP (NAT, oficinas): documentar esta limitación.
6. **Fairness y starvation.** Con locks por usuario, evaluar si hace falta lock "justo" o si basta el comportamiento por defecto. Documentar la decisión.

## 7. Arquitectura propuesta del proyecto

```text
rate-limiter/
├── README.md                  # explicación, decisiones, cómo correr tests y benchmark
├── src/
│   ├── RateLimiter            # interfaz
│   ├── Decision               # resultado de allow()
│   ├── Clock                  # interfaz de reloj (real y falso para tests)
│   ├── NaiveRacyLimiter       # versión con la condición de carrera (demostración)
│   ├── GlobalLockLimiter      # lock global (línea base de performance)
│   ├── PerKeyLimiter          # solución principal (variante A y/o B)
│   ├── LockFreeLimiter        # opcional (variante C)
│   ├── KeyResolver            # estrategia para obtener la clave (IP, header, id)
│   ├── Cleaner                # limpieza de inactivos
│   └── Server                 # servidor HTTP mínimo, un hilo por conexión
├── test/
│   ├── CorrectnessTest        # carrera: nunca más de `limit` aceptadas
│   ├── WindowExpiryTest       # expiración con reloj falso
│   ├── IsolationTest          # claves distintas no se afectan
│   └── CleanupTest            # limpieza sin romper el límite
└── bench/
    └── Benchmark              # throughput vs cantidad de hilos para cada variante
```

El servidor HTTP es solo un cliente de demostración del limiter: un endpoint simple (`GET /`) que pasa por el limiter y responde 200 o 429. No hace falta un framework completo; basta la librería estándar del lenguaje.

## 8. Estrategia de tests

### 8.1 Test de la condición de carrera (el más importante)

- Preparar un limiter con `limit = 5` y una clave `"user-1"`.
- Lanzar `N` hilos (por ejemplo 100 o 1000) que llaman a `allow("user-1")` **al mismo tiempo**, sincronizados con una barrera (`CyclicBarrier`, `CountDownLatch`, `WaitGroup`) para maximizar la contención.
- Contar cuántas respuestas fueron `true`. **Debe ser exactamente 5.**
- Repetir el test miles de veces, porque una carrera puede no aparecer en una sola ejecución.
- La versión `NaiveRacyLimiter` debe **fallar** este test; las versiones correctas deben pasarlo siempre.

### 8.2 Test de la ventana

- Con reloj falso: 5 peticiones aceptadas, la sexta rechazada, avanzar el reloj 10 s, la siguiente se acepta.
- Verificar el comportamiento en el borde exacto (petición justo en `t + window`).
- Verificar que las rechazadas no consumen cupo (RF3).
- Verificar el valor de `retryAfter`.

### 8.3 Aislamiento

- Muchos usuarios en paralelo, cada uno con su propio límite: ninguno debe superar `limit` ni ser bloqueado por la actividad de otro.

### 8.4 Limpieza

- Usuarios inactivos desaparecen del mapa.
- Hilos que llaman a `allow` mientras corre la limpieza nunca producen más de `limit` aceptadas por ventana.

## 9. Benchmark

Medir **peticiones por segundo** para cada variante (global, por clave, lock-free) con 1, 2, 4, 8, 16, ... hilos, en dos escenarios:

1. **Claves distintas** (cada hilo usa su propio usuario): la variante por clave debe escalar con los hilos y superar claramente al lock global.
2. **Una sola clave caliente** (todos los hilos usan el mismo usuario): todas las variantes se serializan en ese usuario; esto sirve para mostrar el límite inherente del problema.

Entregar los resultados en una tabla o gráfico en el README, con una breve interpretación.

## 10. Criterios de aceptación

- [ ] Ningún test de corrección falla en 10.000 repeticiones del test de carrera.
- [ ] `NaiveRacyLimiter` demuestra el bug (el test falla de forma reproducible con suficiente contención).
- [ ] La solución principal **no** usa un lock global.
- [ ] El benchmark muestra que, con claves distintas, la solución principal escala frente al lock global.
- [ ] Hay limpieza de inactivos y no existe la carrera limpieza/uso (sección 6, punto 2).
- [ ] Se usa un reloj monotónico inyectable.
- [ ] El servidor devuelve 429 con `Retry-After` al rechazar.
- [ ] El README explica el problema, la solución elegida, los trade-offs y las limitaciones.

## 11. Fuera de alcance (por ahora)

- Rate limiting **distribuido** entre varios servidores. Como extensión futura: Redis con un script Lua (la operación se ejecuta de forma atómica en el servidor de Redis) o `INCR` + `EXPIRE` para ventana fija.
- Persistencia del estado entre reinicios.
- Autenticación, TLS y otros temas del servidor web.

## 12. Stack tecnológico

- Lenguaje: Java 21
- Gestor de build/tests: Maven

**Java 17+**: es el caso clásico de hilo por conexión, ofrece `ConcurrentHashMap`, `ReentrantLock` y `AtomicReference`, y tiene JUnit y JMH para tests y benchmarks. El diseño es independiente del lenguaje; si se elige otro, usar las primitivas equivalentes.

## 13. Instrucciones para la IA

1. **Antes de codificar**, confirmá el lenguaje y el gestor de build. Si falta información, asumí lo más razonable, dejalo anotado en el README y avanzá.
2. **Trabajá por etapas** y verificá cada una antes de pasar a la siguiente:
   1. Interfaz, `Clock` real y falso.
   2. `NaiveRacyLimiter` + test de carrera que **falla**.
   3. `GlobalLockLimiter` (correcto pero lento).
   4. `PerKeyLimiter` (correcto y escalable) + todos los tests en verde.
   5. Limpieza de inactivos y sus tests.
   6. Servidor HTTP mínimo con 429 y `Retry-After`.
   7. Benchmark y README con resultados.
   8. Opcionales: `LockFreeLimiter`, token bucket, ventana fija.
3. **Ejecutá los tests de verdad.** No des por correcta una implementación concurrente solo por leer el código.
4. **No inventes resultados del benchmark.** Si no podés ejecutarlo, decilo y dejá el benchmark listo para correr.
5. **Explicá las decisiones** de diseño en comentarios breves y en el README, especialmente por qué cada variante es (o no es) segura.
6. Mantené el código simple y legible. Prefiero una solución clara y correcta antes que una sofisticada.

---

## Anexo: glosario

- **Condición de carrera:** el resultado depende del orden en que se intercalan los hilos.
- **Check-then-act / TOCTOU:** se verifica una condición y luego se actúa, pero la condición pudo cambiar en el medio.
- **Sección crítica:** fragmento de código que solo un hilo puede ejecutar a la vez para un mismo recurso.
- **Lock striping:** repartir el estado entre varios locks para reducir la contención.
- **CAS (compare-and-set):** operación atómica que actualiza un valor solo si sigue valiendo lo esperado.
- **Reloj monotónico:** reloj que nunca retrocede, adecuado para medir intervalos.
