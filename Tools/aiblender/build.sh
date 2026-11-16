#!/bin/sh
# Builds the aiblender models and exports them where the scene builder reads them. Stops at the first model that
# fails its build or its check.
set -eu
cd "$(dirname "$0")"
out="../../Assets/DeadCounty/Art/Models/Source"
mkdir -p "$out" renders
[ $# -gt 0 ] || set -- $(find items props -name '*.json' | sed 's/\.json$//' | sort)
for model in "$@"; do
    name=$(basename "$model")
    aiblender apply "$model.json" --scene "$name" --reset + check + render --style beauty --views hero right \
        --out "renders/$name.png" + export --out "$out/$name.glb" > "renders/$name.log" || { cat "renders/$name.log"; exit 1; }
    python3 -I - "renders/$name.log" "$name" <<'PY'
import json, sys
steps = [json.loads(line) for line in open(sys.argv[1])]
warnings = [w for w in steps[0].get("warnings", [])]
export = steps[-1]
size = [round((b - a) * 1000) for a, b in zip(export["bbox"]["min"], export["bbox"]["max"])]
print(f"{sys.argv[2]:18s} {export['tris']:6d} tris  {size} mm  check {'ok' if steps[1]['pass'] else 'FAILED'}")
for w in warnings:
    print("   ", w["code"], w.get("parts", ""), w.get("hint", "")[:140])
for issue in steps[1].get("issues", []):
    print("   !", issue["rule"], issue.get("parts", ""), issue.get("hint", "")[:140])
PY
done
