# Prüfung: Debug Mod 1.1.0

Stand: 3. November 2026. Spielversion: PvZ Replanted 1.5.1469_Steam, Unity 6000.0.52f1, MelonLoader 0.7.3, Proton Experimental. Die Spielstarts und Laufzeittests wurden nach ausdrücklicher Freigabe durchgeführt.

## Installierte Dateien

- `DebugMod.dll` 1.1.0: `0f866855988ad6d5b706b838bd6df6f82eef8b52798fe72d92d90cc5ab902c92`
- `PlantLibrary.dll` 1.0.4: `f7ebd4a28043888eee6e4a353fb8e4b951f9daa7b58008f06e835fa5c281e260`
- `ZombieLibrary.dll` 1.1.4: `5223c26a907322335bf8ec70aa0ce8ba8e4da7f5c0aff3d1a54c146103cfbe2a`

Die installierten DLLs stimmen byteweise mit den Release-Builds überein. Alle 24 vorhandenen Prüfsummeneinträge für die installierten Mods wurden geprüft. Letzte Installation: `backups/install-20261103-132409-660717/installation.json`.

## Erfolgreiche Prüfungen

- Release-Builds für Debug Mod, Plant Library und Zombie Library: jeweils ohne Warnungen oder Fehler.
- Drei Offline-Tests des Python-Clients: fehlendes/veraltetes Lebenszeichen, Anfrage-/Antwortzuordnung und Bereinigung, Timeout mit unklarer Wirkung.
- Bestehende Zombie-Library-Prüfung: `Registry contracts passed.`
- 52 API-Aufrufe im vollständigen Debug-Durchlauf einschließlich erwarteter Fehler: native und registrierte Auswahl/Freischaltungen, Platzierung, Umplatzierung, Entfernen, Ressourcen, Bank, Roster, Zustandsabfragen, Screenshot, FPS, Geschwindigkeit, Einstellungen, Pause und Rückkehr ins Menü.
- Fehlerfälle: noch nicht spielbereites Level, falsche Zelle, ungeeignetes Terrain, alte Boardkennung, inaktiver zweiter Spieler, geschützte Profilspeicherung, ungültige Lautstärke und unbekannter Befehl.
- Protokollprüfung: falsche Protokollversion, alte Session, abgelaufene Anfrage und falscher Argumenttyp.
- Normales Survival-Level: Start, native Pflanzenwahl, Budget 30 mit Normal/TrafficCone/Football, Pflanzenbank, Platzierung, Sonne und Rückkehr ins Menü.
- Minispiel `ChallengeWhackAZombie`: Start bis `Playing`, Zombieabfrage und Rückkehr ins Menü.
- Platzierungen unabhängig vom gewählten Roster, einschließlich leerem Zombieroster. Augenzombie mit getrennten Zuständen sowie tatsächlich fliegende Projektile abgefragt.
- Registrierte Modpflanze „Flammen-Erbse“ über die Debug-Auswahl gewählt und direkt aus dem Modkatalog platziert; nativer Marker `Jalapeno` bestätigt.

Rohantworten stehen unter `validation/`. `tools/smoke_debug_api.py` wiederholt den Hauptdurchlauf im bereits gestarteten Spiel. Weitere Protokoll- und Variantenantworten wurden während der gezielten Laufzeitprüfung gespeichert.

## Grenzen der Prüfung

Die 151 aufgelisteten nativen Leveldefinitionen wurden nicht alle einzeln durchgespielt. Sondermodi können eigene Spawn- und Platzierungsbedingungen besitzen. Die native dauerhafte Profilspeicherung wurde nicht mit einem geänderten Münzstand getestet. Im Debug-Level wurde geprüft, dass `persist: true` gesperrt bleibt; der Münzwert im Arbeitsspeicher wurde unverändert zurückgeschrieben. Grafische Abnahme aller Mod-Almanachseiten und sämtlicher Fenster-/Auflösungsmodi bleibt offen. Fremdmods können zusätzliche Sonderregeln besitzen.

Die Laufzeitprüfung fand und behob fehlerhafte Harmony-Parameternamen, Unitys defekten generierten Screenshot-Wrapper, die Serialisierung nativer Einheiten-IDs, den fehlenden zweiten Spieler und den Menüwechsel über die native Zustandssteuerung. Im abschließenden Spielprotokoll traten keine Fehler oder Ausnahmen auf.

Das Spiel wurde nach den Prüfungen im Hauptmenü belassen.

## Zombieauswahl 1.2.0 — tatsächlich ausgeführt am 03.11.2026

Release-Build ohne Warnungen/Fehler. Im laufenden Spiel wurden 98 thematische Images sowie graue Chooser-/Bankrahmen und Hirne in Kartenkosten/Ressourcenzähler geprüft. Nach Bestätigung verschwinden alle thematischen Images aus der aktiven UI. Ein Abbruch während der Zombieauswahl und eine erneute Pflanzenwahl stellen ebenfalls die native Darstellung wieder her. Die Tests enden im Hauptmenü.

Belege: `references/spreading-21-0-0/zombie-chooser.png`, `zombie-ui.json`, `restored-ui.json` und `abort-runtime.json`. Das API `debug.ui` liest ausschließlich aktive Images innerhalb einer temporären Debug-Sitzung.

## Debug Mod 1.2.1 — Zombiepakete und laufender Roster

Der laufende Debug-Roster verwendet dieselben grauen Bank- und Hirnzähler-Sprites wie die Zombieauswahl. Registrierte Fusion-Zombies erhalten 20 Saatpakete aus transparenten Originalporträts mit demselben 128×176-Hintergrund wie native Zombies. Almanachbilder bleiben unverändert. Der Bildtest prüft jeden exportierten Rahmen und freien Papierbereich gegen die gemeinsame Vorlage.

Release-Build: 0 Warnungen, 0 Fehler. Paketprüfung: 1 Test für alle 20 Karten bestanden. API-Transport: 3 Tests bestanden. Neue Testbefehle `debug.plant.set` und `debug.fertilize` sind auf temporäre Debug-Boards und aktuelle Boardkennungen begrenzt. Der echte Düngerpfad bleibt in Fusion.

## Debug Mod 1.2.2 — abschließend geprüft am 04.11.2026

Spielstart und API-Tests sind seit dem API-Release erlaubt. Steam wurde als Desktopbenutzer kevin gestartet. Die API wartet mit `chooser.ready` auf das sichtbare, vollständig gebundene Auswahlpanel und prüft diesen Zustand vor Änderungen. `debug.difficulty` liest die aktive Schwierigkeit; das Lebenszeichen meldet die tatsächliche Assembly-Version.

Release-Build: 0 Warnungen, 0 Fehler. Der vollständige Durchlauf `../fusion-mod/tools/smoke_puff_duo.py` besteht: Zombieauswahl, laufender Roster, beide Rezeptreihenfolgen beider Pflanzen, Dünger, alte Boardkennung, Boden- und Ballontreffer sowie Haftkirschschaden und verzögerte Explosion. Die aktiven Schwierigkeitseinstellungen wurden berücksichtigt und nicht geändert. Auswahlpakete und laufender Roster wurden visuell geprüft. Belege: `../fusion-mod/references/puff-duo-21-1-0/runtime.json` und die fünf dort gespeicherten PNG-Dateien.

Aktuell installiert: Debug 1.2.2 und Fusion 21.1.1; alle vier installierten DLLs stimmen byteweise mit ihren Release-Builds überein. Prüfsummen und Installationsbackup stehen im zugehörigen `validation.json`. Die temporäre Sitzung wurde verlassen; Spiel im Hauptmenü. Das aktuelle Protokoll enthält keine Fehler oder Ausnahmen.


## 1.2.3: Native Sonne und gespeicherte Effekte

Release-Build und drei Offline-Clienttests bestanden. Der API-Laufzeittest der vier neuen Fusionen verwendet `debug.items` zum Vergleich gespeicherter Schildpunkte nach Produktion und echten Bissen. Die obere Leiste zeigt nach der Zombieauswahl wieder die native rotierende Sonne; die untere Zombieleiste behält ihr Hirnsymbol. Screenshot und Antworten stehen unter [Pumpkin/Glacier-Nachweise](../fusion-mod/references/pumpkin-glacier-21-3-0/verification.json). Der Fix verändert ausschließlich Debug Mod und gibt dem unveränderten Sun Collector die ursprüngliche Bankgrafik für seinen Cache. Die temporären Sitzungen wurden beendet.


## Debug Mod 1.3.0 und Plant Fusion 21.4.0 (2026-11-04)

Die realen Spieltests verwenden die dokumentierte lokale API und temporäre Debug-Boards. `validation/controls-runtime.json` enthält acht abgeschlossene Prüfgruppen: Profile und Erfolge lesen, ungültige Profilmutationen abweisen, Modusfreischaltungen einschließlich Levelstatus, alle fünf Schwierigkeitsgrade, Sun Collector, echte goldene Kaffeebohne, native Grabsteine, individuelle und gemeinsame Rasenmäheroperationen mit allen vier Typen sowie sämtliche Xbox-360-Tasten einschließlich Guide, analoger Sticks und automatischer Freigabe.

Gültige schreibende Profilbefehle wurden zum Schutz vorhandener Profile nicht ausgeführt. Profil- und Erfolgslisten blieben während der Tests unverändert. Der Controller-Test kann mit RS den echten gespeicherten Sun-Collector-Schalter betätigen; der Test stellt anschließend sowohl den Speicherzustand als auch die vorherige Konfigurationsdatei wieder her.

`../fusion-mod/references/treasure-three-mines-21-4-0/runtime.json` dokumentiert 13 abgeschlossene Rezept- und Fähigkeitsprüfungen. Darunter sind die tatsächlichen animierten Übergänge der Kartoffelprojektile aus der Pflanze in die Nachbarreihen, der verzögerte dritte Schuss am Spielfeldrand, 30/5400/1800 Schaden, Rückstoß, erneutes Eingraben, beide Düngerpfade, echte Ressourcen und Karten sowie beschleunigte Schatzfelder und Entfernung nach der dritten Karte. Die Bilder liegen daneben.

Der Startparameter `--debug-level=Day` wurde beim realen Steam-/Proton-Start geprüft. Er erreicht ohne manuelle Auswahl `Playing` mit Peashooter und Normal-Zombie als Platzhaltern. Die neue Shutdown-Prüfung wird getrennt unter `validation/shutdown-runtime.json` archiviert. Release-Builds von Fusion, Debug Mod und Difficulty Mod sind fehlerfrei. Die bestehenden Transporttests bestehen ebenfalls. Native Speicher-/Ladezyklen wurden wegen des Speicherschutzes der Debug-Sitzung nicht ausgeführt; die Projektilkennung und der Rücksetzpfad sind zusätzlich offline geprüft.

Der abschließende Shutdown-Test beendet auch nach Controller-Eingaben den tatsächlichen Spielprozess: `processExited: true` nach 9,41 Sekunden. Vor dem nativen Quit werden die vorhandenen Fusion-Kameraziele über deren Besitzer gelöst. Beim Grafikabbau bleiben drei native RenderTexture-Fehlermeldungen bestehen; der Prozess endet dennoch vollständig. Zusätzlich bestehen 185 Minen-/Projektilprüfungen und 427 bestehende Sonnenblumen-/Düngerprüfungen mit Offline-Doubles.


## Debug Mod 1.3.1 — Karten und Controller, geprüft am 05.11.2026

Release-Build ohne Warnungen oder Fehler; drei API-Transporttests bestanden. Der tatsächliche Controller-Durchlauf besteht 25 Prüfungen: D-Pad und linker Stick, alle acht Pflanzenseiten, Vorwärts-/Rückwärtsblättern mit passender Zeile, vertikale Navigation nach dem Austausch gepoolter Karten, unvollständige letzte Zeile, Kartenbestätigung und beide Auswahlphasen bis zum laufenden temporären Debug-Spiel. Ein beim Phasenwechsel zerstörter Fokus wird wiederhergestellt. Der aktuelle Zombiekatalog umfasst 47 Karten auf einer Seite; Navigation und Bestätigung wurden geprüft, ein zweiter Zombie-Tab ist mangels Einträgen nicht zur Laufzeit prüfbar. Fusion-Karten haben im Screenshot vollständige Saatpaket-Hintergründe.

Belege: [Controller-Antworten](../fusion-mod/references/umbrella-ice-21-5-0/controller-runtime.json), [Fusionskarten](../fusion-mod/references/umbrella-ice-21-5-0/fusion-packets.png), [letzte Pflanzenseite](../fusion-mod/references/umbrella-ice-21-5-0/last-plant-page.png) und [Zombieauswahl](../fusion-mod/references/umbrella-ice-21-5-0/zombie-controller.png). Der reproduzierbare Test liegt unter `../fusion-mod/tools/smoke_chooser_controller.py`. `plant.place offsetY:70` wurde mit beiden Eistopf-Rezeptreihenfolgen über den echten Fusionspfad geprüft.

Die abschließende eigene Testsitzung wurde verlassen, das virtuelle Gamepad entfernt und das Spiel regulär geschlossen; das tatsächliche Prozessende wurde bestätigt. Die installierten DLLs stimmen byteweise mit den Release-Builds überein. Beim Unity-Abbau protokolliert PvZRepatched drei RenderTexture-Meldungen; während der Auswahl- und Kampftests traten keine Ausnahmen auf.
