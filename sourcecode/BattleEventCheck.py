import sys
from threading import Lock
from pathlib import Path
sys.path.insert(0, str(Path("work/eurse-repository/backend/app").resolve()))
from patch import Watcher

class Relay:
    def __init__(self):
        self.allowed = set()
        self.also = set()
        self.trials = {}
        self.lock = Lock()
    def budget_ok(self): return True
    def add_route(self, ip): self.allowed.add(ip); return True
    def remove_route(self, ip): self.allowed.remove(ip)

events = []
relay = Relay()
watcher = Watcher("unused.log", relay, lambda *args, **kwargs: None, events.append)
watcher.read_line('NetworkStep - Changed on "InBattle"')
watcher.read_line('ConnectUpdater.GetAddress completed id=battle host=prod-game-eu-0-battle-g1-gcore-7.rob-run.kefir.games dnsAddresses=1 addresses=1.1.1.1')
watcher.read_line('ConnectUpdater.PeerConnect started id=battle address=1.1.1.1:36650 rtt=60 attempts=0')
watcher.read_line('ConnectUpdater.Connect completed id=region_user')
assert events == ['joining']
watcher.read_line('ConnectUpdater.Connect completed id=battle')
watcher.read_line('ConnectUpdater.Connect completed id=battle')
watcher.move['started'] -= 10
watcher.check_stall()
assert events == ['joining', 'joined'] and not relay.allowed
watcher.read_line('RoomConnection.DisconnectedNetworkPeerPacket battle')
watcher.read_line('NetworkStep - Changed on "InRealmNotInBattle"')
assert events == ['joining', 'joined', 'disconnected', 'left']
watcher.read_line('NetworkStep - Changed on "InBattle"')
watcher.read_line('ConnectUpdater.GetAddress completed id=battle host=prod-game-eu-0-battle-g1-gcore-7.rob-run.kefir.games dnsAddresses=1 addresses=1.1.1.1')
watcher.read_line('ConnectUpdater.PeerConnect started id=battle address=1.1.1.1:36650 rtt=60 attempts=0')
watcher.move['started'] -= 10
watcher.check_stall()
assert relay.allowed == {'1.1.1.1'}
watcher.read_line('ConnectUpdater.Connect completed id=battle')
assert events[-2:] == ['joining', 'joined_route']
watcher.read_line('NetworkStep - Changed on "InRealmNotInBattle"')
assert events[-1] == 'left' and not relay.allowed
print("Direct join, active-route join, duplicate filtering, disconnect and departure passed.")
