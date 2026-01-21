# BlockBattle - Aktueller Stand
## Systemarchitektur & Komponenten

---

# Folie 1: Die Kern-GameObjects

## Übersicht der Hauptkomponenten

Block-System:
- BlockSpawner - Spawnt greifbare Blöcke für den Spieler
- ReferenceStructureSpawner - Spawnt transparentes Hologramm der Zielstruktur

Bauzone & Validierung:
- BuildZone - Definiert den 3D-Bereich wo gebaut werden darf
- BuildValidator - Vergleicht platzierte Blöcke mit Referenzstruktur
- BuildZonePlacementGuides - Zeigt Bodenmarkierungen wo Blöcke hingehören

Debugging:
- ValidationDebugVisualizer - Zeigt Toleranz-Sphären an erwarteten Positionen

UI & Spielfluss:
- GameplayHUD - Zeigt Fortschritt, Block-Status und Level-Anzeige
- LevelManager - Steuert Level-Progression und Übergänge

---

# Folie 2: BlockSpawnConfiguration (ScriptableObject)

## Datendefinition für Levels

Speicherort:
- Assets/BlockBattle/Structures/Level1Structure.asset
- Assets/BlockBattle/Structures/Level2Structure.asset

Struktur eines SpawnEntry:
- BlockType - Cube, Cylinder, Triangle, Rectangle, Arch
- BlockColor - Natural, Red, Green, Yellow, Blue, Orange, DarkGreen
- Position - Relative Position in der Struktur
- Rotation - Ausrichtung des Blocks
- AllowedRotations - Welche Drehungen beim Platzieren erlaubt sind

AllowedRotations - Rotationsregeln:
- FlipX, FlipY, FlipZ → 180° Drehung auf Achse erlaubt
- Steps90X, Steps90Y, Steps90Z → 90° Schritte auf Achse erlaubt

---

# Folie 3: Level-Strukturen im Detail

## Level1Structure.asset (6 Blöcke)

| # | Typ | Farbe | Beschreibung |
|---|-----|-------|--------------|
| 1 | Rectangle | Green | Basis unten |
| 2 | Rectangle | Red | Basis oben |
| 3 | Rectangle | Natural | Stütze links (90° gedreht) |
| 4 | Rectangle | Natural | Stütze rechts (90° gedreht) |
| 5 | Cube | Yellow | Mitte |
| 6 | Triangle | Blue | Dach |

## Level2Structure.asset (9 Blöcke)

| # | Typ | Farbe | Beschreibung |
|---|-----|-------|--------------|
| 1-4 | Cube | Red, Green, Blue, Yellow | 4 Würfel als Fundament |
| 5, 7 | Arch | Orange | 2 Bögen nebeneinander |
| 6, 8 | Rectangle | DarkGreen | 2 Querbalken |
| 9 | Cylinder | Blue | Abschluss oben |

---

# Folie 4: Validierung & Debug-Tools

## BuildValidator

Validierungslogik:
- Findet platzierten Block mit gleichem Typ + Farbe
- Prüft Position (innerhalb konfigurierbarer Toleranz)
- Prüft Rotation (gemäß AllowedRotations des Blocks)
- Berechnet Accuracy = (korrekte Blöcke / alle Blöcke) × 100%

Konfigurierbare Parameter:
- Position Tolerance (Standard: 12cm)
- Rotation Tolerance (Standard: 15°)

## ValidationDebugVisualizer

- Zeigt transparente Sphären an jeder erwarteten Block-Position
- Sphären-Größe = Position Tolerance Radius
- Farbkodierung: Grün (korrekt), Gelb (falsch platziert), Grau (fehlt)
- Linien verbinden erwartete mit tatsächlicher Position bei Fehlern

---

# Folie 5: LevelManager & HUD

## LevelManager - Spielablauf

- Lädt Level1Structure beim Start
- Setzt SpawnConfiguration für BlockSpawner und ReferenceStructureSpawner
- Aktualisiert GameplayHUD mit Level-Nummer
- Bei 100% Accuracy → zeigt "Level Complete!" → lädt nächstes Level
- Nach letztem Level → zeigt "Alle Level geschafft!"

## GameplayHUD

- Level-Anzeige - Zeigt aktuelles Level oben zentriert
- Block-Indikatoren - Leuchten auf bei korrekter Platzierung
- Fortschrittsbalken - Zeigt Gesamtgenauigkeit in Prozent
- Success-Panel - Erscheint bei Level-Abschluss

## BuildZonePlacementGuides

- Zeigt farbige Markierungen auf dem Boden der BuildZone
- Jede Markierung entspricht dem Footprint eines Blocks
- Hilft dem Spieler bei der Orientierung wo Blöcke hingehören
