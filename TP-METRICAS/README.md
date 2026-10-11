# Rate limiter concurrente

Rate limiter exacto de ventana deslizante para Java 21. Permite, por defecto, hasta 5 peticiones aceptadas por clave en cualquier intervalo de 10 segundos, sin serializar la actividad de usuarios distintos.

## Diseño

`RateLimiter<K>` devuelve una `Decision` con `allowed`, `retryAfter` y `remaining`. La implementación principal, `PerKeyRateLimiter`, guarda como máximo `limit` timestamps aceptados por clave y ejecuta el descarte, la verificación y el registro dentro de un único `ConcurrentHashMap.compute`. Esa operación es atómica para la clave afectada: dos peticiones del mismo usuario se serializan, mientras las de claves distintas pueden progresar en paralelo.

La ventana usa `System.nanoTime`, un reloj monotónico que no se ve afectado por cambios de hora o NTP. El reloj se inyecta en tests para controlar el paso del tiempo sin `sleep`. Un timestamp expira exactamente cuando `timestamp <= now - window`.

Las peticiones rechazadas no se registran y, por lo tanto, **no consumen cupo**. `Retry-After` representa el tiempo hasta que expira la petición aceptada más antigua y se redondea hacia arriba a segundos en HTTP.

El proyecto también contiene:

- `GlobalLockRateLimiter`: implementación correcta con un lock global, usada como línea base.
- `NaiveRacyRateLimiter`: implementación deliberadamente incorrecta que separa check y actualización.
- `InactiveKeyCleaner`: limpieza periódica de historiales expirados.
- `RateLimitedHttpServer`: servidor mínimo basado en la biblioteca estándar, con un hilo virtual por tarea.
- `KeyResolver`: estrategia intercambiable para identificar al cliente.

## Corrección de la limpieza

La limpieza usa `computeIfPresent` para la misma clave. No puede eliminar un historial mientras `allow` lo modifica: ambas operaciones se coordinan mediante la atomicidad por clave del mapa. Así se evita que un hilo conserve y actualice un bucket huérfano mientras una petición posterior crea otro bucket.

No se configuró un lock justo. La sección crítica es muy corta, `ConcurrentHashMap` no ofrece fairness por clave y agregar una cola justa aumentaría coordinación y latencia sin garantizar el orden de llegada de las peticiones HTTP.

## Ejecutar

Requisitos: JDK 21 y Maven 3.8 o superior.

```bash
mvn clean test
mvn package -DskipTests
java -cp target/rate-limiter-1.0.0-SNAPSHOT.jar edu.unlam.ratelimiter.Server
```

El servidor escucha en `http://localhost:8080/`; se puede indicar otro puerto como primer argumento. Responde `200` mientras queda cupo y `429` con `Retry-After`, `X-RateLimit-Limit` y `X-RateLimit-Remaining` cuando se agota.

La estrategia predeterminada usa la dirección remota. `HeaderKeyResolver` solo debe habilitarse detrás de un proxy confiable que elimine y reescriba el header: un cliente puede falsificar `X-Forwarded-For`. Limitar por IP también puede agrupar usuarios legítimos detrás de NAT; en producción suele preferirse un id autenticado o API key. Para IPv6 puede ser conveniente normalizar por prefijo `/64` según el modelo de amenaza.

## Tests y demostración de la carrera

La suite normal verifica 10.000 carreras sincronizadas, expiración y borde exacto de ventana, aislamiento de claves, limpieza concurrente y contrato HTTP:

```bash
mvn test
```

El test de la versión ingenua está deshabilitado por defecto porque **debe fallar**. Un hook de test detiene todos los hilos después de leer el mismo estado, haciendo reproducible el interleaving defectuoso:

```bash
mvn -DrunRacyDemo=true -Dtest=NaiveRaceDemonstrationTest test
```

El resultado esperado es un build fallido: se aceptan 20 peticiones donde el assertion permite 5.

## Benchmark

JMH compara lock global y operación por clave con una clave caliente y con una clave distinta por hilo:

```bash
mvn -Pbenchmark -DskipTests clean package
java -jar target/benchmarks.jar -t 8
```

Para comparar escalabilidad, repetir con `-t 1`, `-t 2`, `-t 4`, `-t 8` y `-t 16`.

Los buckets se mantienen llenos durante la medición para acotar memoria y medir el costo de sincronización.

Resultados medidos el 11/10/2026 en la máquina de desarrollo (Windows 11, Oracle JDK 21.0.7, JMH 1.37, un fork). Las cifras son throughput agregado en millones de operaciones por segundo; son una referencia local, no una garantía para otro hardware.

| Hilos | Global, claves distintas | Por clave, claves distintas | Global, clave caliente | Por clave, clave caliente |
|---:|---:|---:|---:|---:|
| 1 | 17,7 M | 12,4 M | 17,6 M | 12,5 M |
| 2 | 4,8 M | 12,9 M | 6,7 M | 6,0 M |
| 4 | 6,7 M | 16,6 M | 7,6 M | 3,6 M |
| 8 | 5,0 M | 20,9 M | 7,9 M | 2,6 M |
| 16 | 6,4 M | 25,1 M | 7,4 M | 3,0 M |

Con claves distintas, la variante por clave creció de 12,4 M a 25,1 M ops/s y a 16 hilos rindió aproximadamente 3,9 veces más que el lock global. Con una clave caliente, `compute` serializa correctamente a los contendientes y el throughput deja de escalar, que es el límite inherente del requisito. JMH conserva el detalle y los intervalos de confianza con `-rf json -rff resultado.json`.

## Complejidad y límites

Cada llamada cuesta O(1) amortizado y cada clave activa conserva como máximo `limit` timestamps. El estado es local al proceso: no se comparte entre réplicas y se pierde al reiniciar. Un rate limiter distribuido requeriría, por ejemplo, una operación atómica en Redis. TLS, autenticación y confianza de proxies quedan fuera del alcance de este ejemplo.
