"""Checks the contract tests notice Dalamud changing: for each mutant, writes a copy of the
installed Dalamud.dll with one behaviour changed (as a future release might), points DALAMUD_HOME
at the copy and runs the contract tests. Every mutant must fail at least one; D0 (unchanged, only
rewritten) must fail none.

    python tests/SafetyTests/Mutation/Dalamud/run.py            # every mutant
    python tests/SafetyTests/Mutation/Dalamud/run.py D2 D13     # just these

Needs the safety tests built (dotnet build tests/SafetyTests -c Release) and room in the temp
folder for a copy of Dalamud's (~200 MB).
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
TESTS = os.path.abspath(os.path.join(HERE, "..", ".."))
TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"

MUTANTS = {
    "D0": "unchanged (baseline, through the same rewrite)",
    "D1": "Framework.Run runs inline on the framework thread",
    "D2": "a disposed Reloaded hook stays enabled",
    "D3": "SafetyHook swallows a failed native enable",
    "D4": "ConditionFlag.Occupied renumbered",
    "D6": "ClientState.TerritoryType follows GameMain every frame",
    "D7": "ClientStatePluginScoped gains a field the plugin's territory sync writes",
    "D8": "TerritoryChanged raised when the territory did not change",
    "D9": "the tick no longer drains Framework.Run's queue",
    "D10": "Update handlers no longer isolated",
    "D11": "leftover plugin hooks not disposed on unload",
    "D12": "SafetyHook backend dropped",
    "D13": "the framework scheduler runs tasks inline as they are queued",
    "D14": "a disposed Reloaded hook's Original stays callable",
    "D15": "Reloaded hooks start enabled",
    "D16": "Update dispatch switched off outside Dalamud's unload",
    "D17": "conditions read from a per-frame cache",
}


def dalamud_home():
    home = os.environ.get("DALAMUD_HOME")
    return home or os.path.join(os.environ["APPDATA"], "XIVLauncher", "addon", "Hooks", "dev")


def main():
    wanted = sys.argv[1:] or list(MUTANTS)
    source = dalamud_home()
    subprocess.run(["dotnet", "build", HERE, "-c", "Release", "-nologo", "-v", "q"], check=True)
    patcher = os.path.join(HERE, "bin", "Release", "net10.0", "DalamudMutants.dll")
    results = []
    with tempfile.TemporaryDirectory(prefix="anomech-dalamud-") as tmp:
        copy = os.path.join(tmp, "dev")
        shutil.copytree(source, copy)
        env = dict(os.environ, DALAMUD_HOME=copy)
        for mid in wanted:
            rec = {"id": mid, "desc": MUTANTS[mid]}
            p = subprocess.run(["dotnet", patcher, mid, os.path.join(source, "Dalamud.dll"), os.path.join(copy, "Dalamud.dll")],
                               capture_output=True, text=True)
            if p.returncode != 0:
                rec["result"] = "patch failed"
                rec["detail"] = p.stderr[-500:]
            else:
                trx_dir = os.path.join(TESTS, "TestResults")
                shutil.rmtree(trx_dir, ignore_errors=True)
                subprocess.run(["dotnet", "test", TESTS, "-c", "Release", "--no-build", "-nologo",
                                "--filter", "FullyQualifiedName~Contract", "--logger", "trx;LogFileName=d.trx"],
                               capture_output=True, text=True, env=env, timeout=900)
                trx = os.path.join(trx_dir, "d.trx")
                if not os.path.exists(trx):
                    rec["result"] = "tests did not run"
                else:
                    failed = [r.get("testName") for r in ET.parse(trx).getroot().iter(TRX_NS + "UnitTestResult") if r.get("outcome") != "Passed"]
                    rec["failed"] = failed
                    rec["result"] = ("BROKEN BASELINE" if failed else "clean") if mid == "D0" else ("caught" if failed else "MISSED")
            results.append(rec)
            print(f"{mid:4} {rec['result']:15} {len(rec.get('failed', [])):2} failing  {rec['desc']}", flush=True)
    out = os.path.join(tempfile.gettempdir(), "anomech-dalamud-mutation-results.json")
    json.dump(results, open(out, "w", encoding="utf-8"), indent=1)
    real = [r for r in results if r["id"] != "D0"]
    print(f"\n{sum(r['result'] == 'caught' for r in real)}/{len(real)} Dalamud changes caught. Details: {out}")


if __name__ == "__main__":
    main()
