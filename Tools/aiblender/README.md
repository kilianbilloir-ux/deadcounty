# Models made with aiblender

Each `.json` here is one model: a list of aiblender ops (`aiblender help`). `build.sh` builds, checks, renders and
exports them as GLB into `Assets/DeadCounty/Art/Models/Source`; the scene builder then turns every GLB into a mesh
(`TestSceneBuilder.Models.cs`). The GLB files are committed, so the game builds without aiblender.

    Tools/aiblender/build.sh                 # every model
    Tools/aiblender/build.sh items/batte     # some of them

Conventions:

- Metres, Z up. Unity gets (x, z, y): what a file writes along +Y lies along Unity's +Z.
- The origin is the pivot: on the ground, in the middle of a prop; at the grip of a weapon, which lies along +Y.
- `set_look preset polygon`: one flat colour per facet. Colours come out as cells of one shared palette texture, so
  roughness and metalness written here are not read by the game.
- The same model in other colours is a line of `ModelVariants` in the scene builder, not another file.
