# Debug Mod 1.1.1: lokale Command-API

## Voraussetzungen und Aufruf

Die installierte `DebugMod.dll` muss Version 1.1.1 haben. Starte PvZ Replanted normal über Steam. Die API wird beim Laden der Mod initialisiert und funktioniert auch unter Proton. Befehle werden im Unity-Hauptthread ausgeführt. Es wird kein Netzwerkport geöffnet.

Der Client liegt im Quellprojekt unter `debug-mod/tools/debug_api.py`. Vom Arbeitsverzeichnis `/home/kevin/modding` aus:

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py status
python3 pvzreplanted/debug-mod/tools/debug_api.py help
python3 pvzreplanted/debug-mod/tools/debug_api.py COMMAND '{"argument":"value"}'
```

Optional: `--game-dir '/anderer/Spielpfad'`, `--timeout 30`. Das zweite Positionsargument ist ein JSON-Objekt. Rückgaben sind JSON. Exitcode 0 bedeutet Erfolg; Exitcode 1 bedeutet Fehler. Vor Spielstart liefert der Client `GAME_NOT_READY`. Während des Starts zeigt `status` `ready: false` und `code: GAME_NOT_READY`. Ein sichtbarer Startbildschirm ist noch kein fertiges Hauptmenü. Mit `startup.continue` lässt sich dessen nativer Knopf „ZUM STARTEN KLICKEN!“ auslösen, auch unter Wayland ohne Eingabeautomation:

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py startup.continue
python3 pvzreplanted/debug-mod/tools/debug_api.py status
```

Der Befehl löst ausschließlich den aktiven, bedienbaren `LoadBar`-Button im nativen `SplashScreenPanel` aus. Lade-, Profil- und Zustandswechsel bleiben beim Spiel. `STARTUP_NOT_READY` bedeutet, dass der Knopf fehlt oder noch nicht bedienbar ist. Nach `accepted: true` auf `status.ready: true` warten. Wiederholung im fertigen Hauptmenü liefert `alreadyReady: true`; während eines Levels wird `INVALID_STATE` gemeldet. Lizenz- und Profildialoge werden nicht automatisch bestätigt.

`accepted: true` bestätigt eine angeforderte Zustandsänderung. Levelwechsel, Intro und Kartenwahl laufen anschließend über das Spiel. Prüfe danach `status` beziehungsweise `chooser`. Ein Timeout beweist nicht, dass eine Mutation ausgeblieben ist. Prüfe den Zustand vor einer Wiederholung.

## Level und Minispiele

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py levels
python3 pvzreplanted/debug-mod/tools/debug_api.py level.start '{"index":0}'
python3 pvzreplanted/debug-mod/tools/debug_api.py level.leave
```

`levels` gibt alle vom Spiel bereitgestellten Leveldefinitionen aus: Adventure, Survival, Rätsel, Minispiele und weitere vorhandene Modi. Verwende deren `index`; Nummern können sich zwischen Spielversionen ändern. `level.start` benötigt das Hauptmenü oder die native Levelauswahl. Die native Auswahl entscheidet über ein vorhandenes gespeichertes Level, Spezial-Intros und den Ladeablauf. Zum Wechsel zuerst `level.leave` verwenden und auf das Hauptmenü warten. Ein gewählter Spezialmodus muss vom Spiel und den installierten Mods unterstützt werden.

## Debug-Level

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.backgrounds
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.start '{"index":0}'
python3 pvzreplanted/debug-mod/tools/debug_api.py chooser
python3 pvzreplanted/debug-mod/tools/debug_api.py chooser.select '{"indices":[0,1,2],"confirm":true}'
```

`debug.backgrounds` liefert Hintergründe und öffnet die bestehende Debug-Auswahl. `debug.start` verwendet deren Index. Der Debug-Level bleibt Besitzer seiner temporären Levelkopie und seines Speicherschutzes.

`chooser` liefert sämtliche aktuellen Auswahlobjekte mit Index, nativem Zustand und gegebenenfalls Modnamen. In der Debug-Sitzung folgen zwei Phasen: zuerst Pflanzen, anschließend Zombies. Warte nach Bestätigung der Pflanzen, bis `chooser` `zombiePhase: true` meldet. Wähle dann die Zombies:

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py chooser.select '{"indices":[0,1],"confirm":true}'
```

Die Zombieauswahl darf leer sein: `{"indices":[],"confirm":true}`. Pro Debug-Leiste sind höchstens zehn Karten möglich. `confirm: false` ändert nur die Auswahl. `chooser.select` funktioniert auch bei der normalen Kartenwahl; dort gelten die native Kapazität und Verfügbarkeit. Versteckte native Pflanzen werden nicht still freigeschaltet.

Im fertigen Debug-Level lässt sich die manuelle Zombieleiste ersetzen:

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py catalog.zombies
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.roster '{"indices":[0,1,2]}'
```

Hier sind die Indizes aus `catalog.zombies.debug` gemeint. Sie unterscheiden sich von den Indizes unter `registered`.

## Pflanzenkarten und Kataloge

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py catalog.plants
python3 pvzreplanted/debug-mod/tools/debug_api.py catalog.zombies
python3 pvzreplanted/debug-mod/tools/debug_api.py bank
python3 pvzreplanted/debug-mod/tools/debug_api.py bank.set '{"plants":["Peashooter","Sunflower","Wallnut"]}'
```

Die Kataloge unterscheiden native Enum-Namen von registrierten Modvarianten. `catalog.plants` benötigt ein vorhandenes Board, weil Kosten und Modkataloge vom Level abhängen können. `bank.set` ersetzt Spieler-0-Karten im laufenden Level durch bis zu zehn native Pflanzen; die tatsächliche Packetkapazität darf nicht überschritten werden. Modvarianten wählst du über `chooser.select` oder platzierst sie direkt über den Katalog. Modkataloge werden beim Aufruf neu ermittelt; verwende Indizes aus derselben Spiel-/Modkonfiguration.

## Platzieren und Umplatzieren

Koordinaten beginnen bei null: `col` ist 0 bis 8; `row` ist 0 bis 4 beziehungsweise 5. Die Rosterwahl begrenzt API-Platzierungen nicht. Regeln für Terrain, Unterstützung und Belegung bleiben aktiv.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py plant.place '{"type":"Peashooter","col":2,"row":1}'
python3 pvzreplanted/debug-mod/tools/debug_api.py zombie.place '{"type":"Normal","col":8,"row":1}'
python3 pvzreplanted/debug-mod/tools/debug_api.py zombie.place '{"type":"Normal","col":8,"row":1,"entrance":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py plant.place '{"registered":0,"col":2,"row":1}'
python3 pvzreplanted/debug-mod/tools/debug_api.py zombie.place '{"registered":0,"col":8,"row":1}'
```

`registered` bezeichnet einen Eintrag aus `catalog.plants.registered` beziehungsweise `catalog.zombies.registered`. Bibliotheken und Fusion bleiben für Varianten, Initialisierung und spezielle Regeln zuständig. Beliebige native Marker werden nicht angenommen. `entrance: true` behält die natürliche Eintrittsposition des Zombies. Ungültige Zellen liefern `INVALID_CELL`; verletzte Platzierungsregeln liefern `INVALID_PLACEMENT` mit Begründung. Nicht jeder Sonderzombie lässt sich außerhalb seines nativen Modus erzeugen. Fehlgeschlagene Zombieerzeugung räumt die neu angelegten Zombies dieses Versuchs auf.

Aktuelle IDs und Boardkennung erhältst du über `plants`, `zombies` und `status`. Verschieben und Entfernen verlangen die passende Boardkennung. Dadurch können alte IDs nach einem Levelwechsel keine anderen Einheiten verändern.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py plant.move '{"board":"BOARDKENNUNG","id":65537,"col":3,"row":1}'
python3 pvzreplanted/debug-mod/tools/debug_api.py zombie.move '{"board":"BOARDKENNUNG","id":65537,"col":7,"row":2}'
python3 pvzreplanted/debug-mod/tools/debug_api.py plant.remove '{"board":"BOARDKENNUNG","id":65537}'
python3 pvzreplanted/debug-mod/tools/debug_api.py zombie.remove '{"board":"BOARDKENNUNG","id":65537}'
```

Lawn Glove besitzt vorhandene Regeln für das Verschieben von Pflanzen und Stapeln. Ohne Lawn Glove unterstützt die API gewöhnliche Einzelpflanzen; abhängige Unterlagen, Sonderzustände und Zweifeldpflanzen werden geschützt. Zombies verwenden die native Reihen-, Pool- und Positionsaktualisierung.

## Ressourcen

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py sun.set '{"amount":5000}'
python3 pvzreplanted/debug-mod/tools/debug_api.py coins.level.set '{"amount":100}'
python3 pvzreplanted/debug-mod/tools/debug_api.py coins.profile.set '{"amount":1000}'
python3 pvzreplanted/debug-mod/tools/debug_api.py coins.profile.set '{"amount":1000,"persist":true}'
```

`sun.set` erlaubt optional `player: 0` oder `1`. Im Debug-Level wird danach die automatische Rücksetzung auf 9990 für diese Sitzung abgeschaltet; dessen kostenlose Platzierung bleibt aktiv.

`coins.level.set` setzt den nativen Levelzähler `mCoinsCollected`. Das ist kein zusätzlicher Geldbeutel und keine Änderung bereits fliegender Münzobjekte. `coins.profile.set` verwendet native Profileinheiten: eine Einheit entspricht zehn angezeigten Münzen. Ohne `persist: true` wird nur der Arbeitsspeicher geändert; spätere native Speichervorgänge können diesen Wert übernehmen. `persist: true` ruft die native Profilspeicherung auf. Während der Debug-Sitzung ist diese explizite Speicherung gesperrt. Erstelle vor dauerhaften Profiländerungen eine Sicherung.

## Zombiebudget und automatische Wellen in normalen Leveln

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py waves
python3 pvzreplanted/debug-mod/tools/debug_api.py waves.set '{"budget":30,"types":["Normal","TrafficCone","Pail"]}'
python3 pvzreplanted/debug-mod/tools/debug_api.py waves.set '{"firstWave":3,"budget":50,"types":["Normal","Football"]}'
```

`budget` sind native Zombiepunkte pro noch nicht gespawnter Welle, keine Sonnenkosten oder Trefferpunkte. Die API füllt jede verbleibende Welle reihum mit den gewählten Typen, solange Punkte reichen. Sie aktualisiert auch die native Zombieauswahl `mZombieAllowed`. Sie meldet ausgegebene Punkte, Restbudget und den fertigen Plan. Das Budget ist eine Obergrenze; unteilbare Restpunkte bleiben übrig. Eine zu große Belegung wird vor dem Schreiben mit `WAVE_CAPACITY` abgelehnt. `firstWave` ist nullbasiert und darf keine bereits gestartete Welle bezeichnen. Ohne Angabe beginnt die Änderung bei der nächsten noch nicht gespawnten Welle (`mCurrentWave`). Bestehende Zombies bleiben bestehen. Spezialmodi und spätere Survival-Stufen können ihre native Tabelle erneut erzeugen. Im Debug-Level sind automatische Wellen abgeschaltet; dort dient `zombie.place` zum Erzeugen.

## Freischalten, Sperren und Almanach

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.plant '{"type":"Peashooter","unlocked":false}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.plant '{"type":"Peashooter","unlocked":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.almanac '{"kind":"plant","type":"Peashooter","unlocked":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.almanac '{"kind":"zombie","type":"Yeti","unlocked":false}'
python3 pvzreplanted/debug-mod/tools/debug_api.py almanac
python3 pvzreplanted/debug-mod/tools/debug_api.py almanac '{"unlockedOnly":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlocks.reset
```

Overrides gelten bis zum Spielende oder `unlocks.reset`; sie verändern keine Adventure-Stufe, Käufe oder Erfolge. Pflanzenauswahl und Almanacheintrag sind getrennte Freischaltungen. Bereits geöffnete Pflanzenauswahlen gegebenenfalls erneut öffnen. Almanachdaten werden aktualisiert. Explizite Sperren haben Vorrang vor dem nativen Debug-Reveal. Für registrierte Varianten aus `almanac.plants` und `almanac.zombies` verwende stattdessen `owner` und `id` aus der Antwort:

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.plant '{"owner":"fusion","id":"ID_AUS_ALMANAC","unlocked":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.almanac '{"kind":"plant","owner":"fusion","id":"ID_AUS_ALMANAC","unlocked":false}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.almanac '{"kind":"zombie","owner":"fusion","id":"ID_AUS_ALMANAC","duck":false,"unlocked":true}'
```

Plant Library 1.0.4 und Zombie Library 1.1.4 besitzen dafür eigene sitzungsbezogene Overrides. Die Almanachlisten aktualisieren sich auch bei geöffnetem Mod-Almanach. Entenvarianten haben getrennte Sperren. `plantUnlocked` kann ohne geladenes Board `null` sein, wenn die normale Freischaltung ein profilabhängiges Board benötigt. Ohne diese Bibliotheksversionen bleiben die nativen API-Befehle verfügbar; Mod-Overrides melden `LIBRARY_API_UNAVAILABLE`.

## Zustandsabfragen

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py board
python3 pvzreplanted/debug-mod/tools/debug_api.py plants
python3 pvzreplanted/debug-mod/tools/debug_api.py zombies
python3 pvzreplanted/debug-mod/tools/debug_api.py projectiles
python3 pvzreplanted/debug-mod/tools/debug_api.py waves
python3 pvzreplanted/debug-mod/tools/debug_api.py fps
```

`board` gibt alle Spielfeldzellen mit Wasserstatus und IDs der dort befindlichen Pflanzen und Zombies aus. Zusätzlich enthält die Antwort Sonne pro aktivem Spieler, Profilmünzen und den Levelmünzzähler. Die vollständigen Einheitenlisten sind enthalten; Zombies außerhalb des sichtbaren Rasters bleiben in der Liste erhalten.

`almanac` enthält getrennte Listen `native`, `plants` und `zombies`. Die beiden letzten Listen liefern auch `owner`, `id` und gegebenenfalls `duck` für registrierte Modvarianten.

Snapshots enthalten native skalare `m*`-Felder und `DataID`: beispielsweise Leben, maximale Leben, Rüstung, Position, Reihe, Phase, Zustand, Statustimer, Zielinformationen und Modmarker. Pflanzenaufladung steht bei den Bankkarten unter `mRefreshCounter`, `mRefreshTime` und `mRefreshing`; Angriffstimer gehören zum jeweiligen Pflanzenobjekt. Zombies enthalten zusätzlich den optionalen Augenzombie-Zustand `awake` und `aggressive`. Projektilsnapshots enthalten unter anderem Typ, Bewegung, Position, Geschwindigkeit und Ziel-ID. Timer und Zähler werden in nativen Einheiten ausgegeben; sie sind nicht automatisch Sekunden. Die verfügbaren Feldnamen hängen von der Spielversion ab. Fehler beim Lesen einzelner Felder werden am jeweiligen Feld ausgewiesen.

`waves` zeigt geplante Typen und den Spawn-Countdown. Noch nicht erzeugte Zombies haben keine Instanzleben, Statuseffekte oder aktive Phase. Solche Werte werden nicht erfunden. Zufalls- oder Spezialspawns außerhalb der Wellentabelle können erst nach ihrer nativen Planung beziehungsweise Erzeugung erscheinen.

`fps` misst gerenderte Bilder über Unitys unskalierte Framezeit, nicht durch externe Framegeneration ergänzte Bilder. `frame` zählt Renderframes; `simulationTick` enthält bei vorhandenem Board den nativen `mMainCounter`, sonst `null`. `tickSeconds` liefert das native Zeitintervall pro Tick. Vergleiche Simulationsticks nur bei gleicher `board`-Kennung. Damit lassen sich Renderleistung und tatsächlich erreichter Simulationsfortschritt getrennt messen.

## Screenshot, Geschwindigkeit und Einstellungen

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py screenshot
python3 pvzreplanted/debug-mod/tools/debug_api.py speed
python3 pvzreplanted/debug-mod/tools/debug_api.py speed '{"index":1}'
python3 pvzreplanted/debug-mod/tools/debug_api.py settings
python3 pvzreplanted/debug-mod/tools/debug_api.py settings.set '{"name":"MusicVolume","value":0.25}'
python3 pvzreplanted/debug-mod/tools/debug_api.py settings.set '{"name":"SoundEffectVolume","value":0.5}'
python3 pvzreplanted/debug-mod/tools/debug_api.py settings.set '{"name":"VSync","value":false}'
python3 pvzreplanted/debug-mod/tools/debug_api.py settings.set '{"name":"Resolution","value":{"width":1920,"height":1080}}'
python3 pvzreplanted/debug-mod/tools/debug_api.py pause '{"paused":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py pause '{"paused":false}'
```

`pause` besitzt genau einen eigenen Eintrag im nativen Pausezähler von `TimeUtil`. Wiederholtes Pausieren addiert keine weiteren Pausen. `paused` und `status.paused` melden `TimeUtil.IsTimePaused()`; `controlPaused` zeigt die API-eigene Pause. Fortsetzen, Boardwechsel, Boardende und API-Abschaltung geben ausschließlich diese eigene Pause frei. Andere Pausebesitzer bleiben erhalten. Die API benötigt dafür keinen sichtbaren Pausenknopf.

Screenshots landen unter `UserData/DebugModApi/screenshots/`. Der Client wartet auf eine vollständige PNG-Datei und gibt deren Linux-Pfad zurück. Direkte Protokollnutzer erhalten zunächst `accepted: true` und müssen das Dateiende selbst abwarten.

`speed` liefert die aktuellen nativen Geschwindigkeitsstufen und deren Indizes; installierte Speed-Mod-Erweiterungen sind enthalten. `speed` mit `index` verwendet die native Zeitsteuerung. Spielpause und bestehende Verlangsamung behalten deren Zuständigkeit; die Antwort enthält den wirksamen Wert der nativen Zeitsteuerung. Bei CPU-Überlast kann die tatsächlich erreichte Simulation langsamer sein; das lässt sich über `fps.simulationTick` und `fps.tickSeconds` messen.

`settings` liefert alle nativen Einstellungen mit Typ, Schreibbarkeit und Enum-Werten. `settings.set` verwendet native Setter einschließlich deren Ereignissen und Speicherverhalten. Unterstützt sind Ton, Musik, Sprache, Fenstermodus, Auflösung, VSync, Vibration, hoher Kontrast, halbe Geschwindigkeit, Bildschirmwackeln, GameplaySpeed, Controllergeschwindigkeit und weitere schreibbare skalare Einstellungen. Lautstärken liegen zwischen 0 und 1. Historische Werte `PreviousResolution` und `PreviousWindowMode` bleiben geschützt. Änderungen können vom Spiel dauerhaft gespeichert werden.

## Dateiprotokoll für andere Clients

Basisverzeichnis: `<Spiel>/UserData/DebugModApi/`. `status.json` enthält Protokollversion, Modversion, Spielsession, PID und `updatedAt` in Unix-Millisekunden. Lebenszeichen älter als fünf Sekunden gelten als nicht verfügbar. `owner.lock` verhindert zwei API-Besitzer im selben Spielverzeichnis.

1. Lies die aktuelle `session` aus `status.json`.
2. Erzeuge eine UUID als 32 Hexzeichen ohne Bindestriche.
3. Schreibe JSON zunächst als temporäre Datei. Benenne sie atomar zu `requests/<UUID>.json` um.
4. Lies die passende Datei aus `responses/<UUID>.json`. Entferne die Antwort nach dem Lesen.

```json
{
  "protocol": 1,
  "session": "AKTUELLE_SESSION",
  "expiresAt": 1793710800000,
  "command": "sun.set",
  "args": {"amount": 5000}
}
```

`expiresAt` muss in der nahen Zukunft liegen. Abgelaufene Anfragen werden ohne Ausführung abgelehnt. Neustarts erzeugen eine neue Session; alte Anfragen liefern `STALE_SESSION`. Die maximale Anfragegröße ist 64 KiB. Pro API-Durchlauf werden höchstens vier Anfragen verarbeitet. Benötigt ein Client eine bestimmte Reihenfolge, wartet er auf jede Antwort, bevor er den nächsten Befehl sendet. Mehrere Befehle bilden keine Transaktion.

```json
{"protocol":1,"id":"UUID","session":"SESSION","ok":true,"result":{"amount":5000}}
```

```json
{"protocol":1,"id":"UUID","session":"SESSION","ok":false,"error":{"code":"INVALID_PLACEMENT","message":"NeedsPot"}}
```

Wichtige Fehlercodes sind `GAME_NOT_READY`, `BOARD_NOT_READY`, `LEVEL_NOT_READY`, `MAIN_MENU_REQUIRED`, `CHOOSER_NOT_READY`, `INVALID_ARGUMENT`, `INVALID_CELL`, `INVALID_PLACEMENT`, `ENTITY_NOT_FOUND`, `STALE_BOARD`, `STALE_SESSION`, `REQUEST_EXPIRED`, `UNKNOWN_COMMAND`, `DEBUG_SAVE_PROTECTED`, `SAVE_FAILED` und `COMMAND_FAILED`. Die API nimmt keine beliebigen Methodenaufrufe, Shellbefehle oder Ausgabepfade an. Zugriff ist auf Prozesse mit Dateirechten im lokalen Spielverzeichnis begrenzt.


## Prüfung und Installation

```sh
# Build ohne Spielstart:
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/debug-mod/DebugMod.csproj -c Release
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/plant-library/PlantLibrary.csproj -c Release
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/zombie-library/ZombieLibrary.csproj -c Release
rtk proxy python3 pvzreplanted/debug-mod/tools/install.py --only DebugMod PlantLibrary ZombieLibrary

# Offline-Clienttests ohne Spielstart:
python3 -m unittest discover -s pvzreplanted/debug-mod/tools -p test_debug_api.py -v

# Bewusster Laufzeittest im bereits gestarteten Spiel, ausgehend vom Hauptmenü:
python3 pvzreplanted/debug-mod/tools/smoke_debug_api.py
```

Der Laufzeittest startet eine temporäre Debug-Sitzung, verändert deren Inhalt und beendet sie wieder. Er prüft unter anderem Auswahl, Platzierung, Umplatzierung, ungültige Eingaben, Ressourcen, native und registrierte Freischaltungen, Screenshot, Einstellungen und Geschwindigkeit. Der Test schreibt seine JSON-Antworten nach `debug-mod/validation/debug-runtime.json`. Er fordert keine dauerhafte Profilspeicherung an. Zusätzliche manuelle beziehungsweise ausgeführte Prüfungen stehen in `debug-mod/VALIDATION.md`.

Der Installer ersetzt ausschließlich die ausgewählten DLLs, aktualisiert betroffene Prüfsummen und sichert vorherige Dateien unter `debug-mod/backups/install-*`. Er startet das Spiel nicht. Zum Rückbau bei geschlossenem Spiel die Dateien anhand der dortigen `installation.json` wiederherstellen. Für die ursprüngliche DebugMod.dll 1.0.0 liegt der Backupstand unter `backups/install-20261103-125712-879568`. Die ursprünglichen Bibliotheken und deren Prüfsummen wurden unter `backups/install-20261103-130931-042599` gesichert. Bei einem vollständigen Rückbau beide Manifeste beachten; spätere Sicherungen enthalten Zwischenstände der API-Erweiterung.

## Reproduzierbare Kampflast im Debug-Level

`debug.zombie.set` verlangt eine aktuelle Boardkennung, eine aktive Zombie-ID und eine temporäre Debug-Sitzung. `health` setzt Körperleben und maximale Körperleben auf 1 bis 10000000. `freezeTicks` setzt nach nativer Vereisung deren Dauer auf 1 bis 1000000 Simulationsticks; 0 entfernt die Vereisung nativ. Mindestens eines der beiden Felder ist erforderlich. Beide Werte werden vor der ersten Änderung geprüft. Rüstung bleibt separat. Boardwechsel schützen alte IDs wie bei `zombie.move`.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.zombie.set '{"board":"BOARDKENNUNG","id":65537,"health":1000000,"freezeTicks":100000}'
```

Damit bleiben Testziele während FPS-Vergleichen stehen und überleben viele Projektile. Die Sitzung verändert keine Spielstände.
