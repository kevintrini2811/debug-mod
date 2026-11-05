# Debug Mod 1.1.0

Eigenständige MelonLoader-Mod für PvZ Replanted. Der Debug-Levelbetrieb wurde aus Plant Fusion 20.0.4 ausgelagert. Die Mod funktioniert auch ohne Fusion, Plant Library, Zombie Library, Lawn Glove oder BloomEngine.

## Bedienung

Im Hauptmenü `debug` tippen. Hintergrund auswählen, anschließend bis zu zehn Pflanzenkarten und bis zu zehn Zombiekarten wählen. Pflanzenkarten dürfen nicht leer bleiben; die Zombieauswahl darf leer bleiben. Zusätzliche Seiten sind über die Pfeile erreichbar. Eine Zombiekarte im Level auswählen und auf das Spielfeld klicken. Rechtsklick oder Escape bricht die Platzierung ab. Am rechten Rand bleibt der natürliche Reiheneintritt erhalten.

Die Sitzung bietet unbegrenzte Sonne, sofortige Kartenaufladung, den verfügbaren Almanach und bei installiertem Lawn Glove dessen sofortige Aufladung. Automatische Sonnen- und Zombiewellen, Niederlage, Levelabschluss und Zombiebeute bleiben im Debug-Level gesperrt. Die Umgebungsauswahl einschließlich Pool, Dach und nebellosem Nachtpool bleibt erhalten.

## Bibliotheken und optionale Mods

Beim Öffnen der Debug-Auswahl werden alle aktuell über `PlantLibrary.Plants.All` und `ZombieLibrary.Zombies.All` registrierten Einträge übernommen. Auch normalerweise gesperrte Modpflanzen und noch nicht gesichtete Modzombies erscheinen. Große Kataloge erhalten zusätzliche Seiten. Die Grenze von zehn gewählten Karten gilt pro Leiste, nicht für den Katalog. Nach späteren Registrierungen die Debug-Auswahl erneut öffnen.

Plant Library liefert Namen, Bilder, Kosten und Platzierungsregeln. Varianten behalten ihre native Identität und ihren Marker. Beim Pflanzen bleibt `Board.AddPlant` zuständig; dadurch bleiben die Initialisierungs- und Tick-Hooks der registrierenden Mods wirksam. Zombie Library erzeugt Modzombies über `Zombies.Spawn`; deren Initialisierung, Marker und Spawn-Sound bleiben erhalten. Reine Entenvarianten erscheinen ebenfalls und können nur in Wasserreihen erzeugt werden.

Bei installiertem Fusion bleiben dessen Zutatenkarten, fertige Fusionen, Fütterung, Terrainregeln und importierte Vorschauen erhalten. Fusion besitzt weiterhin diese Gameplayregeln; Debug Mod besitzt Auswahl, Sitzung und Bank. Es gibt keinen zweiten Debug-Levelbesitzer in Fusion.

Debug Mod erklärt alle derzeit installierten Mods als optionale Abhängigkeiten. Die sieben eigenen Mods erklären umgekehrt `DebugMod` als optional. Integrationen verwenden native Spieltypen, Standarddelegates und zur Laufzeit gebundene Methoden; Debug Mod hat keine Assembly-Referenz auf andere Mods. Die übrigen eigenen Mods haben keine Assembly-Referenz auf Debug Mod. Bestehende Abhängigkeiten der anderen Mods untereinander bleiben erhalten. Fremdmods wie BloomEngine, ReplantAPI und PvZRepatched werden nicht verändert und benötigen Debug Mod weiterhin nicht.

Für die Kataloganbindung gehören die aktualisierten Plant Library 1.0.4 und Zombie Library 1.1.4 zu diesem Release. Fehlen die Bibliotheken vollständig, bleibt die native Auswahl verfügbar.

## Speicherschutz

Der Debug-Level verwendet eine temporäre Kopie der Leveldefinition. Vorherige Levelauswahl und native UI werden beim Verlassen wiederhergestellt. Die bisherigen Sperren für native Board-, Profil- und Levelspeicherung sowie das Laden oder Löschen vorhandener Spielstände wurden übernommen. Die Mod schreibt keine eigenen Fortschrittsdateien. API-Befehle für reguläre Level folgen den nativen Speicherregeln; `coins.profile.set` kann außerhalb der Debug-Sitzung mit `persist: true` ausdrücklich das Profil speichern. Bestehende Zusatzdateien anderer Mods behalten deren Regeln, etwa bereits implementierte Fusion-Freischaltungen.

## Build und Installation

Vom Verzeichnis `/home/kevin/modding` aus:

```sh
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/debug-mod/DebugMod.csproj -c Release
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/fusion-mod/FusionMod.csproj -c Release
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/difficulty-mod/DifficultyMod.csproj -c Release
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/sun-collector-mod/SunCollector.csproj -c Release
rtk proxy python3 pvzreplanted/debug-mod/tools/install.py
```

Fusion baut die aktualisierten Bibliotheken, Lawn Glove und Speed Mod mit. Die Installation sichert alle ersetzten DLLs und Prüfsummen unter `backups/install-*`, verwendet die vorhandenen Release-Sperren und prüft die installierten Bytes. Bei Schreibfehlern stellt sie die vorherigen Dateien wieder her. Sie startet weder das Spiel noch Testprogramme. `installation.json` dokumentiert Pfade und SHA-256-Werte. Zum Rückbau bei geschlossenem Spiel die dort angegebenen Sicherungsdateien auf ihre ursprünglichen Pfade kopieren; Dateien mit `backup: null`, insbesondere die neue `DebugMod.dll`, entfernen. Den vollständigen DLL-Satz gemeinsam zurücksetzen.

Der ursprüngliche Quellstand liegt unter `backups/source-*`. Die nativen Zombieporträts und optionale Methodenbindung liegen als gemeinsame Quellen unter `../shared/`. Die Fusion-Werkzeuge `build_debug_cards.py` und `build_debug_zombies.py` aktualisieren die Debug-Assets weiterhin offline.

## Manuelle Abnahme

Die ursprüngliche Auslagerung wurde nur per Build geprüft. Die API-Erweiterung wird gesondert in `src/documentation.md` und `VALIDATION.md` dokumentiert. Weitere manuelle Abnahme:

- Debug Mod allein: Hintergrundauswahl, Pflanzenwahl, Zombieauswahl und Rückkehr zum Menü.
- Gesamter Modsatz: Fusion-Zutaten, fertige Fusionen, Handschuh, Vorschauen und unbegrenzte Ressourcen.
- Weitere Bibliotheksmods: registrierte Pflanzen und Zombies auch ohne normale Freischaltung; bei großen Katalogen Seitenwechsel und Auswahl über Seitengrenzen.
- Platzieren und Entfernen mehrerer Varianten desselben nativen Typs; Initialisierung und Verhalten des registrierenden Mods.
- Reguläres Level nach dem Verlassen: normale Speicherung, Wellen, Beute und Freischaltungen.

## Lokale Command-API

Version 1.1.0 ergänzt eine JSON-Dateischnittstelle für das laufende Spiel. Der Python-Client funktioniert auf dem Linux-Host auch bei Proton. Befehle, Beispiele, Fehlercodes und Speicherregeln stehen in [src/documentation.md](src/documentation.md).
