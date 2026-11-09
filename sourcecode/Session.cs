using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Eurse {
    enum Scenario { Party, Solo, Transient, Expired }
    enum Stage { Idle, Queued, Waiting, Stalled, Probe, Reconcile, Resume, Backoff, Joined, Returned }
    sealed class Session {
        public event Action<string, string, string> Log = delegate {};
        public Stage State { get; private set; }
        public Scenario Mode { get; private set; }
        public string Reservation { get; private set; }
        public string Party { get; private set; }
        public int Generation { get; private set; }
        public int Attempt { get; private set; }
        public int Members { get { return Mode == Scenario.Solo ? 1 : 4; } }
        public int Time { get; private set; }
        public bool Auto { get; set; }
        public bool ServerReady { get { return State != Stage.Idle && Time >= 11 && Time < Lease; } }
        public bool Waiting { get { return State == Stage.Waiting || State == Stage.Stalled; } }
        public bool Active { get { return State != Stage.Idle && State != Stage.Joined && State != Stage.Returned; } }
        public int Lease { get { return Mode == Scenario.Expired ? 22 : 150; } }
        public const int Deadline = 120;
        private int next;
        private int issuedGeneration;
        private string credential;
        private string recoveryKey;
        private bool listenerAttached;
        private bool readyPublished;

        public void Reset() {
            State = Stage.Idle;
            Time = 0;
            Reservation = null;
            Party = null;
        }

        public void Start(Scenario scenario) {
            Mode = scenario;
            Time = 0; Attempt = 0; Generation = 0; issuedGeneration = 0;
            listenerAttached = false; readyPublished = false;
            credential = null;
            Reservation = "EU-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            Party = Members == 1 ? null : "PTY-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            recoveryKey = Guid.NewGuid().ToString("N").Substring(0, 8);
            State = Stage.Queued;
            Log("INFO", "QUEUE", "Route eu-central-2 selected | cohort=" + (Members == 1 ? "solo" : "party/4"));
        }

        public void Tick() {
            if (!Active) return;
            Time++;
            if (Time == 5) {
                State = Stage.Waiting;
                Generation = Members == 1 ? 1 : 41;
                Log("INFO", "RESERVE", "Seat allocated | id=" + Reservation + " | lease=" + Lease + "s");
                if (Party != null)
                    Log("INFO", "PARTY", "Context pinned | party=" + Party + " | generation=41 | member=02/04");
            }
            if (Time == 11) {
                readyPublished = true;
                if (Party != null) issuedGeneration = 42;
                else issuedGeneration = 1;
                Log("TRACE", "GATEWAY", "SESSION_READY published | seq=8842 | listener=" + (listenerAttached ? "active" : "pending"));
            }
            if (Time == 14) {
                listenerAttached = true;
                Log("TRACE", "LISTENER", "Status stream attached | cursor=8843 | replay=disabled");
            }
            if (Time == 22 && Mode == Scenario.Expired)
                Log("WARN", "RESERVE", "Reservation lease expired | seat released by coordinator");
            if (Time >= Deadline) {
                Return("MM_STATUS_TIMEOUT", "Client deadline elapsed. Returning to map.");
                return;
            }
            if (State == Stage.Waiting && Time >= 26) {
                State = Stage.Stalled;
                Log("WARN", "WATCHDOG", "MM_EU_204 | WAITING FOR STATUS | SESSION_READY acknowledgement missing");
            }
            if (State == Stage.Stalled && Auto) Recover();
            if (Time < next) return;
            switch (State) {
                case Stage.Probe: Verify(); break;
                case Stage.Reconcile: Reconcile(); break;
                case Stage.Resume: Resume(); break;
                case Stage.Backoff: RecoverAttempt(); break;
            }
        }

        public void Recover() {
            if (State != Stage.Stalled) return;
            Log("INFO", "RECOVERY", "RecoverActiveSession | reservation=" + Reservation + " | auth=client-session");
            RecoverAttempt();
        }

        private void RecoverAttempt() {
            Attempt++;
            State = Stage.Probe;
            next = Time + 5;
            Log("TRACE", "VERIFY", "GET reservation state | attempt=" + Attempt + "/3 | route=eu-central-2");
        }

        private void Verify() {
            if (Mode == Scenario.Transient && Attempt == 1) {
                Retry("503 COORDINATOR_UNAVAILABLE", 10);
                return;
            }
            if (Time >= Lease) {
                Return("410 RESERVATION_EXPIRED", "Authoritative state EXPIRED. Returning to map.");
                return;
            }
            if (!ServerReady || !readyPublished) {
                Retry("425 RESERVATION_PENDING", 6);
                return;
            }
            Log("OK", "VERIFY", "200 OK | authoritative=READY | client=WAITING_FOR_STATUS | seq=8842");
            State = Stage.Reconcile;
            next = Time + 7;
        }

        private void Reconcile() {
            if (Party != null)
                Log("INFO", "PARTY", "Reconcile " + Party + " | generation " + Generation + " -> " + issuedGeneration + " | roster=4/4");
            Generation = issuedGeneration;
            credential = Reservation + "/" + (Party ?? "SOLO") + "/" + Generation + "/" + (Members == 1 ? "01" : "02");
            Log("OK", "AUTH", "Member ticket validated | scope=join:resume | generation=" + Generation + " | ticket=********");
            State = Stage.Resume;
            next = Time + 8;
            Log("TRACE", "HANDSHAKE", "ResumeJoin dispatched | reservation=" + Reservation + " | idempotency=" + recoveryKey);
        }

        private void Resume() {
            if (Time >= Lease) {
                Return("410 RESERVATION_EXPIRED", "Lease ended before JoinAck. Returning to map.");
                return;
            }
            string expected = Reservation + "/" + (Party ?? "SOLO") + "/" + issuedGeneration + "/" + (Members == 1 ? "01" : "02");
            if (credential != expected) {
                Return("409 GENERATION_MISMATCH", "Ticket does not match the active reservation.");
                return;
            }
            Join("JoinAck accepted | SERVER_CONNECTION=ESTABLISHED");
        }

        private void Retry(string reason, int delay) {
            if (Attempt >= 3) {
                Return(reason, "Retry budget exhausted. Returning to map.");
                return;
            }
            State = Stage.Backoff;
            next = Time + delay;
            Log("WARN", "RETRY", reason + " | backoff=" + delay + "s | retaining original reservation");
        }

        public void RestartClient() {
            if (State != Stage.Stalled) return;
            Log("WARN", "CLIENT", "Client restart simulated | bootstrap=RecoverActiveSession");
            if (!ServerReady) {
                Return("410 RESERVATION_EXPIRED", "Bootstrap did not find a live seat. Returning to map.");
                return;
            }
            Log("OK", "VERIFY", "Active reservation found | authoritative=READY");
            if (Party != null && Generation != issuedGeneration) {
                Return("409 PARTY_GENERATION_MISMATCH", "Bootstrap restored generation " + Generation + " | expected=" + issuedGeneration + " | returning to map");
                return;
            }
            Join("Bootstrap reconnect accepted | SERVER_CONNECTION=ESTABLISHED");
        }

        private void Join(string message) {
            State = Stage.Joined;
            Log("OK", "TRANSPORT", message);
            Log("OK", "SESSION", "SESSION RECOVERY SUCCESSFUL | " + (Party == null ? "solo reservation retained" : "party integrity preserved | roster=4/4"));
        }

        private void Return(string reason, string detail) {
            State = Stage.Returned;
            Log("ERROR", "SESSION", reason + " | " + detail);
        }
    }
}

