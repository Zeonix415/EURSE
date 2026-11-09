# EURSE

**EURSE credits** · UUsataire · Kacpix · eyesshut · damianull

EURSE is a Windows app that attempts to recover stalled battle joins in ROB RUN. Its diagnostics console shows live messages from the patch.

## Download and use

1. Open **Releases** and download `EURSE.exe` from the **Assets** section.
2. Open ROB RUN, then launch `EURSE.exe`.
3. Click **Start patch** and approve the Windows administrator prompt.
4. When you're done, click **Safe stop**. If you're still in a battle using the relay, leave the battle normally so EURSE can remove the temporary route.

The **Battle connection** panel tracks when a join starts and completes. After a successful join, it shows **Connected** and indicates whether the EURSE route was active.

EURSE is experimental and temporarily changes Windows network routes. Compatibility with Easy Anti-Cheat has not been verified.

## Code provenance

The backend is based on **ROB RUN Connection Patch 0.3.2**, which is licensed under the MIT License. The EURSE integration adds support for running without a terminal window and removes the demo entry point. The code that detects stalled connections, manages routes, and modifies ENet packets comes from that version.

The original copyright notice and MIT License are in `backend/app/LICENSE`. The Python and Wintun licenses are in `backend/app/runtime/LICENSE.txt` and `backend/app/WINTUN-LICENSE.txt`.
