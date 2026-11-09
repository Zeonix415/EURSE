# EURSE

EURSE to aplikacja Windows do uruchamiania obejścia problemu z dołączaniem do bitwy w ROB RUN. Pokazuje rzeczywiste komunikaty procesu patcha w konsoli diagnostycznej. **Start patch** wymaga zgody administratora. **Safe stop** prosi backend o zatrzymanie po bezpiecznym zwolnieniu trasy. Jeśli przekaźnik nadal obsługuje bitwę, wyjdź z niej w grze.

Program jest eksperymentalny. Zmienia tymczasowe trasy sieciowe Windows. Zgodność z Easy Anti-Cheat pozostaje niezweryfikowana.

## Budowanie

Na Windows x64 uruchom `build.ps1` w PowerShellu. Skrypt używa lokalnego kompilatora .NET Framework i zapisuje gotowy plik w `build/EURSE.exe`. Python i Wintun są w repozytorium. Budowanie i uruchamianie patcha nie wymagają połączenia z GitHubem.

- `src/` zawiera interfejs EURSE i kod uruchamiający backend.
- `backend/app/` zawiera kod patcha i jego zależności.
- `assets/` zawiera tło i ikonę.
- Skrypt budowania tworzy manifest SHA-256, pakuje backend i osadza go w EXE. Launcher ponownie sprawdza pliki i podpisy bibliotek przed uruchomieniem Pythona.

## Pochodzenie kodu

Backend pochodzi z **ROB RUN Connection Patch 0.3.2** autorstwa **tayvin21** i jest objęty licencją MIT. Zmiany integracyjne dodają tryb bez terminala i usuwają uruchamianie demonstracji. Kod wykrywania zawieszonego połączenia, routingu i modyfikowania pakietów ENet pochodzi z tej wersji.

Informację o prawach autorskich zawiera `backend/app/LICENSE`. Licencje Pythona i Wintun są w `backend/app/runtime/LICENSE.txt` i `backend/app/WINTUN-LICENSE.txt`.
