# Debug Mod 1.3.4: lokale Command-API

## Voraussetzungen und Aufruf

Die installierte `DebugMod.dll` muss Version 1.3.4 haben. Starte PvZ Replanted normal über Steam. Die API wird beim Laden der Mod initialisiert und funktioniert auch unter Proton. Befehle werden im Unity-Hauptthread ausgeführt. Es wird kein Netzwerkport geöffnet.

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

`chooser` liefert sämtliche aktuellen Auswahlobjekte mit Index, nativem Zustand und gegebenenfalls Modnamen. `ready: true` bestätigt zusätzlich ein fertig gebundenes, vollständig angezeigtes Panel ohne fliegende Karten. Warte darauf vor `chooser.select`; `visible` allein genügt nicht. Zu frühe Auswahl meldet `CHOOSER_NOT_READY`, bevor Karten geändert werden. In der Debug-Sitzung folgen zwei Phasen: zuerst Pflanzen, anschließend Zombies. Warte nach Bestätigung der Pflanzen, bis `chooser` `zombiePhase: true` meldet. Wähle dann die Zombies:

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

## Temporäre Zombieauswahl und UI-Prüfung

Debug Mod 1.2.2 ersetzt während der Zombieauswahl die nativen Dekorationen durch graue Kopien. Kartenkosten und Ressourcenzähler verwenden das vorhandene Hirn. Bestätigung und Abbruch stellen alle ausgeliehenen Image-Eigenschaften wieder her; gemeinsame native Texturen bleiben unverändert.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.ui
```

`debug.ui` verlangt eine temporäre Debug-Sitzung (`DEBUG_REQUIRED` außerhalb). Es liefert aktive Unity-Images mit `path`, `sprite`, `color` (RGBA), `width` und `height`. Der Befehl verändert weder UI noch Spielzustand. Thematische Sprites tragen das Präfix `DebugZombieChooser.`. Screenshot und UI-Snapshot können die Darstellung und deren Wiederherstellung getrennt prüfen.

## Pflanzenzustand und Dünger im temporären Debug-Level

`debug.plant.set` verlangt die aktuelle Boardkennung, eine lebende Pflanzen-ID und `health` zwischen 1 und den aktuellen maximalen Lebenspunkten. Der Befehl verändert weder maximale Lebenspunkte noch Fähigkeitstimer. `debug.fertilize` verlangt dieselbe Boardkennung sowie gültige `col`-/`row`-Koordinaten. Er ruft den echten Düngerpfad von Plant Fusion auf; Heilung, Animation und Entwicklung bleiben dessen Verantwortung. Beide Befehle sind außerhalb temporärer Debug-Sitzungen gesperrt. Ohne Fusion meldet `debug.fertilize` `FUSION_API_UNAVAILABLE`.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.plant.set '{"board":"BOARDKENNUNG","id":65537,"health":100}'
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.fertilize '{"board":"BOARDKENNUNG","col":2,"row":2}'
```

Die Zombieleiste im laufenden Debug-Level verwendet dieselben exportierten grauen Roster- und Hirnzähler-Sprites wie die Zombieauswahl. Native Layoutmaße bestimmen Positionen und Kartengröße. Registrierte Fusion-Zombies verwenden Saatpakete aus transparenten Originalporträts mit demselben Hintergrund wie die nativen Zombies. Ihre Almanachrahmen bleiben im Almanach.

`debug.difficulty` liest ausschließlich den für das aktuelle Debug-Board wirksamen Schwierigkeitsgrad aus Difficulty Mod. Ohne zuständige Difficulty Mod meldet es `Normal` (Wert 3). Es verändert weder Sitzung noch gespeicherte Vorgaben. Damit lassen sich Schadenserwartungen bei bestehenden Modifikatoren prüfen. Das API-Lebenszeichen meldet die tatsächlich geladene Debug-Mod-Version.


## Gespeicherte Effekte im temporären Debug-Level

`debug.items` liefert die aktuelle Boardkennung und skalare native Felder aller lebenden `GridItem`-Objekte. Der Befehl ist ausschließlich in temporären Debug-Sitzungen verfügbar und verändert keine Werte. Damit lassen sich beispielsweise gespeicherte Schildpunkte und ihre vollständige Empfänger-DataID vor und nach echten Treffern vergleichen. Feldkennungen bleiben Eigentum der jeweiligen Mod.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.items
```

Debug Mod 1.2.3 gibt dem Sun Collector beim Zuschneiden seines rotierenden Symbols die ursprüngliche native Sonnengrafik. Die temporäre Hirngrafik der Zombieauswahl gelangt dadurch nicht in dessen Cache. Die Änderung liegt vollständig in Debug Mod; Sun Collector bleibt unverändert.


## Direktstart für automatisierte Tests (1.3.0)

Starte Steam als Desktopbenutzer mit dem gewünschten Hintergrund:

```sh
rtk proxy runuser -u kevin -- env DISPLAY=:0 XDG_RUNTIME_DIR=/run/user/1000 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/1000/bus steam -applaunch 3654560 --debug-level=Day
```

Auch `--debug-level Night`, `Pool`, `Fog`, `Roof` sowie ein Index aus `debug.backgrounds` sind möglich. Der Parameter öffnet nach dem Laden ausschließlich eine temporäre Debug-Sitzung und bestätigt beide Kartenwahlen mit je einem Platzhalter: Peashooter und Normal-Zombie. Danach können `bank.set`, `debug.roster` und die Platzierungsbefehle die Testinhalte ersetzen. Warte auf `status.debug: true` und `status.scene: Playing`. Der Parameter überspringt keine Profil- oder Lizenzdialoge. Ungültige Hintergründe werden protokolliert; normale Boards werden niemals ersetzt.

## Spielende und optionale Mods

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py game.shutdown
python3 pvzreplanted/debug-mod/tools/debug_api.py difficulty
python3 pvzreplanted/debug-mod/tools/debug_api.py difficulty '{"board":"BOARDKENNUNG","value":3}'
python3 pvzreplanted/debug-mod/tools/debug_api.py suncollector
python3 pvzreplanted/debug-mod/tools/debug_api.py suncollector '{"enabled":false}'
python3 pvzreplanted/debug-mod/tools/debug_api.py goldenbean.apply '{"board":"BOARDKENNUNG","id":65537}'
```

`game.shutdown` schreibt zuerst die Antwort und ruft drei Renderframes später Unitys reguläres `Application.Quit` auf. Native Speicherregeln gelten weiter; temporäre Debug-Sitzungen behalten ihren Speicherschutz.

`difficulty` benötigt eine installierte Difficulty Mod und deren laufendes Board. Werte: 1 Easy, 2 Medium, 3 Normal, 4 Harder, 5 Extreme. Änderung benötigt die aktuelle Boardkennung. Difficulty Mod 1.0.6 erlaubt auch Änderungen in normalen Leveln; ein so geänderter Lauf erhält keinen Difficulty-Pokal. Frühere Versionen erlauben ausschließlich temporäre Debug-Boards. Die gespeicherte Standardauswahl bleibt unverändert. `debug.difficulty` behält seine bisherige Diagnosefunktion einschließlich Normal-Fallback ohne Mod.

`suncollector` benötigt Sun Collector. Änderung setzt dessen tatsächlichen Schalter und löscht einen vorherigen Fehlerzustand. Die API speichert die Einstellung nicht ausdrücklich; ein späterer nativer Mod-Schalter kann sie speichern. `goldenbean.apply` verwendet den echten Plant-Fusion-Pfad einschließlich Geldbeutel, Kosten, Entwicklung und Ultimate. Es verbraucht keine künstlich erzeugte Bohne. Ungeeignete Pflanzen liefern `applied:false`. Fehlende optionale Mods liefern `MOD_NOT_INSTALLED` beziehungsweise `FUSION_API_UNAVAILABLE`.

## Freischaltungen und Erfolge

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.modes '{"mode":"all","unlocked":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.modes '{"mode":"minigames","unlocked":false}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlocks
python3 pvzreplanted/debug-mod/tools/debug_api.py achievements
```

`mode` akzeptiert `minigames`, `puzzle`, `survival` oder `all`. Die Overrides gelten für Kategorien und ihre Level bis `unlocks.reset`, Profilwechsel oder Spielende. Sie verändern keine nativen Fortschrittswerte. Bereits geöffnete Menüs gegebenenfalls erneut öffnen. `unlocks` liefert Kategorien, alle nativen Saattypen, Objekte und Level sowie die vorhandenen nativen und registrierten Almanachfreischaltungen. `achievements` liefert für jeden nativen Erfolg ID, Namen, `earned` und `shown`; es schaltet keine Erfolge frei.

## Profile verwalten

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py profiles
python3 pvzreplanted/debug-mod/tools/debug_api.py profile.create '{"name":"Testprofil"}'
python3 pvzreplanted/debug-mod/tools/debug_api.py profile.switch '{"guid":"PROFIL-GUID"}'
python3 pvzreplanted/debug-mod/tools/debug_api.py profile.rename '{"guid":"PROFIL-GUID","name":"Neuer Name"}'
python3 pvzreplanted/debug-mod/tools/debug_api.py profile.delete '{"guid":"PROFIL-GUID"}'
```

`profiles` zeigt alle Profile und den aktiven Index. Mutationen verlangen das Hauptmenü ohne Board und ohne Debug-Sitzung. Profile werden anhand ihrer aktuellen GUID adressiert. Namen müssen eindeutig sein, 1 bis 32 Zeichen enthalten und dürfen keine Steuer- oder Pfadzeichen enthalten. Die nativen Dienste besitzen Erstellung, Auswahl, Speicherung und Löschung. Umbenennen und Löschen stellen danach das vorher aktive Profil wieder her, sofern es noch existiert. Das letzte Profil bleibt geschützt. Vor dem Löschen werden vorhandene native Profildateien unter `UserData/DebugModApi/profile-backups/` gesichert. Diese Befehle ändern dauerhafte native Daten; Laufzeittests dürfen bestehende Benutzerprofile nicht verändern.

## Grabsteine, Rasenmäher und Eingabe

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py gravestone.place '{"board":"BOARDKENNUNG","col":6,"row":2}'
python3 pvzreplanted/debug-mod/tools/debug_api.py mowers
python3 pvzreplanted/debug-mod/tools/debug_api.py mower.add '{"board":"BOARDKENNUNG","row":2}'
python3 pvzreplanted/debug-mod/tools/debug_api.py mower.remove '{"board":"BOARDKENNUNG","id":65537}'
python3 pvzreplanted/debug-mod/tools/debug_api.py mower.set '{"board":"BOARDKENNUNG","all":true,"type":"SuperMower"}'
python3 pvzreplanted/debug-mod/tools/debug_api.py mower.remove '{"board":"BOARDKENNUNG","all":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py mower.add '{"board":"BOARDKENNUNG","all":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py controller '{"buttons":["A","LB","Guide"],"leftStick":{"x":0.5,"y":0},"rightTrigger":0.75,"milliseconds":100}'
python3 pvzreplanted/debug-mod/tools/debug_api.py controller.reset
```

Grabsteine verwenden native Platzierungsregeln. Rasenmäher akzeptieren `id`, `row` oder `all:true`; Hinzufügen verwendet `row` oder `all:true`, überspringt vorhandene Mäher und räumt bei Allokationsfehlern die neu erzeugten Mäher auf. Typen: `Lawn`, `Pool`, `Roof`, `SuperMower`. Typänderungen erzeugen vor dem Entfernen des alten Mähers einen Ersatz mit passender nativer Grafik und übertragen Position sowie Laufzustand. Dabei ändert sich die DataID. Änderungen liefern den aktuellen Pool mit vollständigen DataIDs zurück. Alle Board-Mutationen verlangen eine aktuelle Boardkennung.

`controller` erzeugt ein eigenes virtuelles Unity-Gamepad. Tasten: `A`, `B`, `X`, `Y`, `LB`, `RB`, `LT`, `RT`, `Back`, `Start`, `LS`, `RS`, `DPadUp`, `DPadDown`, `DPadLeft`, `DPadRight`, `Guide`. `Guide` ist als zusätzlicher Button verfügbar; Unitys normale Gamepad-Aktionen binden ihn nicht automatisch. Beide Sticks akzeptieren `x`/`y` zwischen -1 und 1, `leftTrigger`/`rightTrigger` zwischen 0 und 1. Jeder Aufruf ersetzt den vollständigen simulierten Zustand. Nach 16 bis 10000 Millisekunden (Standard 100) werden alle Tasten losgelassen; `controller.reset` entfernt das eigene Gerät. `controller.status` liest den tatsächlich im nativen Input-System angekommenen Gerätezustand einschließlich aller Tasten und Achsen. `controller.status` zeigt zusätzlich Geräteaktivierung, Hintergrundfähigkeit und den aktuellen Spielfokus. Echte Controller bleiben unverändert. Die native Gerätezuordnung und die vorhandenen Spielaktionen entscheiden über die Wirkung. Aktionen, die im Spiel selbst Fokus verlangen, benötigen weiterhin das aktive Spielfenster.

## Zusätzliche Nachweise für die Minenports

`coins` liefert skalare native Felder aller lebenden Münzen, Sonnen und Pflanzenkarten. `debug.treasure.set` markiert oder entfernt ein gespeichertes Schatzfeld ausschließlich im temporären Debug-Level. Das unsichtbare Feld dient dem Originalverhalten der Marigold Mine; es verändert keinen nativen Spielstand.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py coins
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.treasure.set '{"board":"BOARDKENNUNG","col":2,"row":2,"enabled":true}'
```


## Kartendarstellung und Controller-Navigation (1.3.1)

Fusion-Pflanzen verwenden im Debug-Katalog die exportierten Saatpakete einschließlich Hintergrund. Pflanzenbibliothek und reguläre Auswahl bleiben Besitzer ihrer eigenen Darstellungen. In beiden Debug-Kartenwahlen blättert eine horizontale Controllerbewegung am Kartenrand weiter beziehungsweise zurück. Die neue Seite erhält den Fokus in der nächsten passenden Zeile. Nach dem Austausch der Karten werden die nativen Rasterverweise erneuert; ein beim Phasenwechsel zerstörter Kartenfokus wird wiederhergestellt. Am Anfang und Ende bleibt die normale native Navigation erhalten.

`chooser` liefert zusätzlich `page`, `lastPage` und `selection` mit dem aktuellen Unity-Objektnamen und dem gewählten Kartenindex. Die Diagnose verändert weder Seite noch Fokus. Sie erlaubt die Prüfung mit dem dokumentierten virtuellen `controller`, einschließlich D-Pad und linkem Stick.


`plant.place` akzeptiert zusätzlich `offsetY` zwischen 0 und 79 (Standard 40) als vertikale Position innerhalb des Felds. Verwende `offsetY:70` für eine Fusion mit einem Blumentopf als Unterlage. Die Fusion-Mod setzt ausschließlich für den synchronen API-Aufruf einen eigenen Cursor-Kontext und stellt ihn auch bei Fehlern wieder her. Dadurch hängt die API-Platzierung nicht von einer zufälligen Mausposition ab. Andere Pflanzenmods verwenden weiterhin ihren eigenen Platzierungscallback.

## Sichtungsschutz und Ballon-Landung (1.3.2)

Debug Mod meldet eigene Boards über `Zombies.SuppressSightings` an Zombie Library 1.2.0. Gesehene native Zombies, Mod-Zombies und Entenvarianten werden dort weder dauerhaft freigeschaltet noch im Sitzungscache als gesehen gespeichert. Die temporäre vollständige Almanachansicht bleibt erhalten. Vorhandene Sichtungen werden nicht verändert.

`debug.balloon.pop` verlangt die aktuelle Boardkennung, eine lebende Zombie-ID und ein temporäres Debug-Level. Der Zombie muss ein fliegender Ballonzombie sein. Der Befehl ruft den echten nativen `LandFlyer`-Pfad auf; Animation, Fall und Wassereintritt bleiben bei den zuständigen Spielsystemen. Andere Zombies und bereits gelandete Ballons liefern `INVALID_ARGUMENT`.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.balloon.pop '{"board":"BOARDKENNUNG","id":65537}'
```

`zombie.place` und `zombie.move` versetzen bei einem nativen Bobteam dessen vorhandene Mitglieder zusammen mit dem Anführer. Dadurch kann der Wassereintritt den vollständig positionierten Verband vom Schlitten lösen.

Positive `freezeTicks` werden vor jeder Mutation abgelehnt, wenn der native Zombie im aktuellen Zustand nicht eingefroren werden kann. Dadurch erhält insbesondere ein fliegender Ballonzombie keinen künstlichen Eis-Timer ohne native Eisgrafik. Verwende für dessen Landetest `debug.balloon.pop`.

## Projektilgrafik prüfen (1.3.3)

`projectiles` akzeptiert optional `visuals:true`. Jeder Projektil-Snapshot enthält dann zusätzlich `visuals` mit den tatsächlichen Renderern des zugehörigen Controllers. Gemeldet werden Hierarchiepfad, Aktivität, Rendererfreigabe, Sprite, Textur, Renderertyp und Weltposition. Der Befehl verändert keine Renderer. Native Schatten können neben importierter Projektilgrafik aktiv bleiben. Damit lassen sich ein korrekter gespeicherter Projektiltyp und die tatsächlich sichtbare Konversion getrennt prüfen.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py projectiles '{"visuals":true}'
```

## Zombie-Grafikdiagnose (1.3.4)

`zombies` akzeptiert optional `visuals:true`. Die erweiterte Antwort enthält je Zombie `state` mit dem bisherigen Snapshot und `rendering` mit Renderern sowie der tatsächlichen Spine-Zeichenreihenfolge. Slots liefern Attachment, Alpha, Bone-Matrix und verfügbare Regionmaße. Renderer liefern Freigabe, Aktivität, Sortierung, Sprite und Weltmaße. Die Diagnose verändert keine Grafik und wird nur auf ausdrückliche Anfrage berechnet.

Alle exportierten nativen Zombieporträts erhalten Debug-Saatpakete, einschließlich zusätzlicher Entenvarianten. Native Bestätigungsknöpfe behalten ihre eigene Zustandsdarstellung; der periodische Dekorationsscan überschreibt ihre Hintergrundgrafik nicht mehr.
