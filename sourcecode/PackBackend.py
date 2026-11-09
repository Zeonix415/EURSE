from pathlib import Path
import hashlib, zipfile

root = "ROB-RUN-Connection-Patch/"
with zipfile.ZipFile("work/robrun-runtime-v0.3.2.zip") as original:
    files = {}
    for entry in original.infolist():
        name = entry.filename[len(root):]
        if name.startswith("app/runtime/") or name in {
                "app/patch.py", "app/transport.py", "app/diagnostics.py", "app/tracking.py",
                "app/console.py", "app/LICENSE", "app/THIRD-PARTY.md",
                "app/WINTUN-LICENSE.txt", "app/wintun.dll", "app/VERSION"}:
            files[name] = original.read(entry)
    source = files["app/patch.py"].decode().replace("\r\n","\n")
    source = source.replace("import demo\n", "")
    a = source.index("def run_demo(opt):")
    b = source.index("def version_text", a)
    source = source[:a] + source[b:]
    source = source.replace("        if console.enable_vt():", "        if not opt.eurse_headless and console.enable_vt():")
    source = source.replace("        else:\n            threading.Thread(target=request_stop", "        elif not opt.eurse_headless:\n            threading.Thread(target=request_stop")
    source = source.replace("    parser.add_argument('--demo', action='store_true')\n    parser.add_argument('--demo-speed', type=float, default=1.0)\n", "    parser.add_argument('--eurse-headless', action='store_true')\n")
    source = source.replace("    if opt.demo:\n        return run_demo(opt)\n", "")
    compile(source, "patch.py", "exec")
    files["app/patch.py"] = source.encode()
    manifest = "".join(f"{hashlib.sha256(data).hexdigest()}  {name}\n" for name,data in sorted(files.items())).encode()
    files["app/SHA256SUMS.txt"] = manifest
    out = Path("work/eurse-robrun-backend.zip")
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as result:
        for name,data in sorted(files.items()):
            entry = zipfile.ZipInfo(root+name)
            entry.compress_type = zipfile.ZIP_DEFLATED
            result.writestr(entry,data)
    print("Package",hashlib.sha256(out.read_bytes()).hexdigest())
    print("Manifest",hashlib.sha256(manifest).hexdigest())
    for name in ("patch.py","transport.py","console.py","diagnostics.py","tracking.py","LICENSE","THIRD-PARTY.md","WINTUN-LICENSE.txt"):
        directory=Path("work/eurse-backend-source")
        directory.mkdir(exist_ok=True)
        (directory/name).write_bytes(files["app/"+name])
