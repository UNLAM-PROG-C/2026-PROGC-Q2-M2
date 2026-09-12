import random
import threading
import time
import sys

clone_number = int(sys.argv[1])

## DEFINE FUNCTIONS

def entrenar_clon(numero_clon, resultados):
    chakra = random.randint(5, 10)  #randint(5, 10) genera un número aleatorio entre 5 y 10
    nivel_clon = 0

    while chakra > 0:
        duracion_intento = random.uniform(0.1, 0.2) # uniform(0.1, 0.2) genera un número aleatorio entre 0.1 y 0.2
        time.sleep(duracion_intento)    

        if random.random() < 0.5:   # random.random() genera un número aleatorio excluyendo 0 y 1
            nivel_clon += 1

        chakra -= 1

    resultados[numero_clon] = nivel_clon

## DEFINE VARIABLES AND THREADS

resultados = [0] * clone_number
hilos = []

##  PROCESS THREADS

inicio = time.time()

for numero_clon in range(clone_number):
    hilo = threading.Thread(
        target=entrenar_clon,
        args=(numero_clon, resultados)
    )

    hilos.append(hilo)
    hilo.start()

for hilo in hilos:  # Esperar a que todos los hilos terminen antes de continuar
    hilo.join()

nivel_naruto = sum(resultados)

fin = time.time()

tiempo_ejecucion = fin - inicio

print("Nivel alcanzado:", nivel_naruto)
print(f"Tiempo de ejecución: {tiempo_ejecucion:.3f} segundos")