import os
import random
import time
import sys

duration = int(sys.argv[1])
frequency = int(sys.argv[2])

## FUNCTIONS

def generate_event(events, probability):
    return random.choices(
        events,
        weights=probability
    )[0]

def monitor_zone(zone, events, probability, duration, frequency):
    inicio = time.time()
    total_events = 0
    total_critic = 0

    while time.time() - inicio < duration:
        event = generate_event(events, probability)

        print(f"[{zone}] - {event}")

        total_events += 1
        if event in events_criticos:
            total_critic += 1

        time.sleep(frequency)
    return total_events, total_critic

## VECTORS/DICTIONARY PRE-INITIALIZATION

events_tiranosaurio = [
    "Todo normal",
    "Tiranosaurio fuera del recinto",
    "Falla en el cerco eléctrico"
]

probability_tiranosaurio = [0.80, 0.10, 0.10]


events_velociraptores = [
    "Todo normal",
    "Pérdida de visibilidad",
    "Falla en el cerco eléctrico"
]

probability_velociraptores = [0.70, 0.20, 0.10]


events_triceratops = [
    "Todo normal",
    "Comportamiento inusual",
    "Estampida"
]

probability_triceratops = [0.60, 0.30, 0.10]


events_visitantes = [
    "Todo normal",
    "Pérdida de comunicación",
    "Alerta de seguridad"
]

probability_visitantes = [0.80, 0.15, 0.05]


events_laboratorio = [
    "Todo normal",
    "Falla del sistema",
    "Pérdida de comunicación",
    "Acceso no autorizado"
]

probability_laboratorio = [0.80, 0.10, 0.05, 0.05]

configurations = {
    "Tiranosaurio": (
        events_tiranosaurio,
        probability_tiranosaurio
    ),

    "Velociraptores": (
        events_velociraptores,
        probability_velociraptores
    ),

    "Triceratops": (
        events_triceratops,
        probability_triceratops
    ),

    "Visitantes": (
        events_visitantes,
        probability_visitantes
    ),

    "Laboratorio": (
        events_laboratorio,
        probability_laboratorio
    )
}

zones = [
    "Tiranosaurio",
    "Velociraptores",
    "Triceratops",
    "Visitantes",
    "Laboratorio"
]

events_criticos = [
    "Tiranosaurio fuera del recinto",
    "Falla en el cerco eléctrico",
    "Pérdida de comunicación",
    "Alerta de seguridad"
]

## Processing

for zone in zones:

    events, probability = configurations[zone]

    pid = os.fork()

    if pid == 0:
        total_events, total_critic = monitor_zone(
            zone,
            events,
            probability,
            duration,
            frequency
        )

        print(f"[{zone}] - events totales: {total_events}, events críticos: {total_critic}")

        os._exit(0)

for _ in range(5):
    os.wait()