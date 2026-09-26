"""Mutation check for the safety tests: copies the repository, then for each mutant in mutants.py
applies its edits, builds the plugin (Release) and the safety tests, runs every test and records
which failed. A mutant nothing catches is a gap in the tests (or a check another one covers).

    python tests/SafetyTests/Mutation/run.py            # every mutant
    python tests/SafetyTests/Mutation/run.py A02 B14    # just these

Needs the same setup as the tests themselves (.NET, Dalamud's dev assemblies). Takes about 15s per
mutant; the per-test details are written to the temp folder.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

import mutants

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


def copy_repo(target):
    shutil.copytree(REPO, target, ignore=lambda d, names: [n for n in names if n in ("bin", "obj", ".git", "TestResults", ".vs")])


def read(path):
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    return raw[3:].decode("utf-8") if bom else raw.decode("utf-8"), bom


def write(path, text, bom):
    open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))


def apply(work, mutant):
    originals = {}
    for e in mutant["edits"]:
        path = os.path.join(work, e["file"])
        text, bom = read(path)
        originals.setdefault(path, (text, bom))
        crlf = "\r\n" in text
        find, repl = (s.replace("\r\n", "\n") for s in (e["find"], e["replace"]))
        if crlf:
            find, repl = find.replace("\n", "\r\n"), repl.replace("\n", "\r\n")
        if text.count(find) != 1:
            raise RuntimeError(f"{e['file']}: the edit matches {text.count(find)} times; update mutants.py")
        write(path, text.replace(find, repl), bom)
    return originals


def run(work, *cmd, timeout):
    p = subprocess.run(list(cmd), cwd=work, capture_output=True, text=True, timeout=timeout)
    return p.returncode, p.stdout + p.stderr


def failures(trx):
    if not os.path.exists(trx):
        return None
    return [r.get("testName") for r in ET.parse(trx).getroot().iter(TRX_NS + "UnitTestResult") if r.get("outcome") != "Passed"]


def main():
    wanted = set(sys.argv[1:])
    selected = [m for m in [{"id": "M0", "kind": "baseline", "desc": "unmodified copy", "edits": []}] + mutants.PAST + mutants.NEW
                if not wanted or m["id"] in wanted or m["id"] == "M0"]
    results = []
    with tempfile.TemporaryDirectory(prefix="anomech-mutation-") as tmp:
        work = os.path.join(tmp, "repo")
        copy_repo(work)
        for m in selected:
            rec = {"id": m["id"], "kind": m["kind"], "desc": m["desc"]}
            originals = {}
            try:
                originals = apply(work, m)
                code, out = run(work, "dotnet", "build", "AnoMech.sln", "-c", "Release", "-p:Platform=x64", "-nologo", "-v", "q", timeout=900)
                if code != 0:
                    rec["result"] = "does not build"
                    rec["detail"] = [l for l in out.splitlines() if " error " in l][:5]
                else:
                    trx_dir = os.path.join(work, "tests", "SafetyTests", "TestResults")
                    shutil.rmtree(trx_dir, ignore_errors=True)
                    run(work, "dotnet", "test", "tests/SafetyTests", "-c", "Release", "-nologo", "--logger", "trx;LogFileName=m.trx", timeout=1800)
                    failed = failures(os.path.join(trx_dir, "m.trx"))
                    if failed is None:
                        rec["result"] = "tests did not run"
                    elif m["kind"] == "baseline":
                        rec["result"] = "BROKEN" if failed else "clean"
                        rec["failed"] = sorted(failed)
                    else:
                        rec["result"] = "caught" if failed else "equivalent" if m["id"] in mutants.EQUIVALENT else "SURVIVED"
                        rec["failed"] = sorted(failed)
            except Exception as ex:
                rec["result"] = "runner error"
                rec["detail"] = [str(ex)]
            finally:
                for path, (text, bom) in originals.items():
                    write(path, text, bom)
            results.append(rec)
            print(f"{rec['id']:4} {rec['result']:17} {len(rec.get('failed', [])):3} failing  {rec['desc']}", flush=True)
    out = os.path.join(tempfile.gettempdir(), "anomech-mutation-results.json")
    json.dump(results, open(out, "w", encoding="utf-8"), indent=1)
    real = [r for r in results if r["kind"] != "baseline" and r["result"] != "equivalent"]
    print(f"\n{sum(r['result'] == 'caught' for r in real)}/{len(real)} mutants caught; baseline {results[0]['result']}. Details: {out}")


if __name__ == "__main__":
    main()
