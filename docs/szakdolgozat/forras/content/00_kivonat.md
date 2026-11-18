#! Tartalmi kivonat

A számítógépes reakcióidőmérést a kijelző megjelenítési késleltetése, az operációs rendszer eseménykezelése és a beviteli eszköz késése torzítja, így a két szoftveres időbélyeg különbsége a mérőlánc saját késleltetését is tartalmazza. A dolgozat egy vizuális ingerekre adott manuális reakcióidőt mérő, hardveresen támogatott rendszer tervezését és megvalósítását mutatja be.

Az inger megjelenését BPW34 fotodióda érzékeli transzimpedancia-erősítőn keresztül, a választ az egérgomb elektromos jele adja. A két jelet ESP32-S3 (N16R8) mikrokontroller időbélyegzi közös, mikroszekundumos időalapon, ESP-IDF alapú firmware-rel. A kijelző soronkénti kirajzolásából adódó időeltérést (scanout-korrekció) a PC-s alkalmazás számítja ki, és próbánként elküldi a mikrokontrollernek. A .NET 10 alapú vezérlőalkalmazásban a mérés paraméterei testreszabhatók, a próbák érvényességét előre rögzített szabályok döntik el, a tekintetet Tobii Pro szemmozgáskövető ellenőrzi. A mérőhardver nyomtatott áramkörre és 3D nyomtatott házba került.

A fejlesztés közben rögzített mérésekben a szoftveres (PC) és a hardveres (ESP) reakcióidő különbsége átlagosan 43,1 ms volt (szórás: 8,1 ms, n = 60), ami nagyságrendileg megegyezik a TMDK-dolgozatban közölt 47,1 ms-os értékkel. A célzott ellenőrző méréssel még igazolandó állításokat a dolgozat külön jelöli.

**Kulcsszavak:** reakcióidő, rendszerkésleltetés, fotodióda, ESP32-S3, ESP-IDF, scanout, szemmozgáskövetés, Tobii, .NET
