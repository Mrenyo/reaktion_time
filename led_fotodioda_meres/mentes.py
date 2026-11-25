# Használat: python mentes.py COM5
import sys
import time
from datetime import datetime
import serial

ser = serial.Serial()
ser.port = sys.argv[1] if len(sys.argv) > 1 else "COM5"
ser.baudrate = 115200
ser.timeout = 10           # egy mérés kb. 1,5 s; ennyi csend után hiba
ser.dtr = ser.rts = False  # megnyitáskor ne resetelje az ESP-t
ser.open()
time.sleep(0.5)
ser.reset_input_buffer()
ser.write(b"START\n")

fajl = f"meres_{datetime.now():%Y%m%d_%H%M%S}.txt"
szamok = []
with open(fajl, "w") as f:
    while True:
        sor = ser.readline().decode(errors="replace").strip()
        if sor == "":
            sys.exit(f"Nem jön adat az ESP-től (eddig {len(szamok)} mérés, mentve: {fajl}).")
        if sor == "VEGE":
            break
        if sor.lstrip("-").isdigit():  # az esetleges indulási üzeneteket átugorja
            szamok.append(int(sor))
            f.write(sor + "\n")
            f.flush()
            print(f"{len(szamok)}: {sor} µs")
ser.close()
print("Mentve:", fajl)
