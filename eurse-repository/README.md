# EURSE

**EURSE credits** · UUsataire · Kacpix · eyesshut · damianull

EURSE is a Windows app that attempts to recover stalled battle joins in ROB RUN. Its diagnostics console shows live messages from the patch. **Start patch** requires administrator approval. **Safe stop** asks the patch to shut down once it can safely remove the temporary route. If you're still in a battle using the relay, leave the battle in-game first.

The **Battle connection** panel tracks when a join starts and completes. After a successful join, it shows **Connected** and indicates whether the EURSE route was active.

EURSE is experimental and temporarily changes Windows network routes. Compatibility with Easy Anti-Cheat has not been verified.

## Building

On Windows x64, run `.\build.ps1` in PowerShell. The script uses the local .NET Framework compiler and saves the executable to `build/EURSE.exe`. Python and Wintun are included in the repository. Building and running the patch do not require access to GitHub.

- `src/` contains the EURSE interface and the code that launches the backend.
- `backend/app/` contains the patch code and its dependencies.
- `assets/` contains the background image and app icon.
- The build script creates a SHA-256 manifest, packages the backend, and embeds it in the executable. The launcher verifies the files and library signatures again before starting Python.

## Code provenance

The backend is based on **ROB RUN Connection Patch 0.3.2**, which is licensed under the MIT License. The EURSE integration adds support for running without a terminal window and removes the demo entry point. The code that detects stalled connections, manages routes, and modifies ENet packets comes from that version.

The original copyright notice and MIT License are in `backend/app/LICENSE`. The Python and Wintun licenses are in `backend/app/runtime/LICENSE.txt` and `backend/app/WINTUN-LICENSE.txt`.