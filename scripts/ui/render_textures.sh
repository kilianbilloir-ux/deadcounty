#!/bin/zsh
# Renders scripts/ui/textures.html into Assets/DeadCounty/UI/Textures with a headless Chrome.
# name:width:height per texture; the sizes must match those in textures.html.
set -e
here=${0:A:h}
out="$here/../../Assets/DeadCounty/UI/Textures"
chrome=${CHROME:-/Applications/Google Chrome.app/Contents/MacOS/Google Chrome}
for spec in sign:1024:512 paper:512:512 tape:512:128 panel:512:512 grime:2048:1024 stroke:512:64; do
  name=${spec%%:*}; size=${spec#*:}
  file="$out/Worn_${(C)name}.png"
  "$chrome" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --default-background-color=00000000 \
    --allow-file-access-from-files --window-size=${size%%:*},${size##*:} --screenshot="$file" "file://$here/textures.html?t=$name" >/dev/null 2>&1
  print "$file"
done
