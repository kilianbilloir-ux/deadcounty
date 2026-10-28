using System.Linq;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Traces of the evacuation on the village houses, readable from the street: search marks sprayed by the front
    // door of the houses that were checked, HELP on the wall and the roof of the barricaded house O3, KEEP OUT on
    // E5, the side and back windows of both boarded up too, police tape torn across the porch of O3. Separate seed
    // from the other passes; none of it is solid.
    public static partial class TestSceneBuilder
    {
        static void BuildEvacuationTraces(Transform parent)
        {
            var rng = new System.Random(93);
            var root = Group(parent, "Traces_Evacuation");
            // Same frame as the house body (see House): front wall on z = 0 facing -Z, walls on x 0..12 and z 0..10.
            Transform Body(int lot, bool east) => Group(root, $"Maison_{(east ? "E" : "O")}{lot + 1}", LotPoint(lot, east, -6f, 0f), LotYaw(east));
            const float paint = -WallThickness / 2f - ClapboardProud - 0.01f;

            foreach (var (lot, east, victims) in new[] { (0, false, "0"), (3, false, "0"), (1, true, "2"), (2, true, "0") })
                MeshObject("Marque_Fouille", Body(lot, east), SearchMarkMesh(victims), new Vector3(7.2f, 1.55f, paint), Quaternion.identity, Vector3.one, new[] { "Spray" }, false);

            var help = Body(2, false);
            Label(help, "HELP", new Vector3(7.1f, 1.65f, paint), Vector3.back, 0.12f, "SignRed");
            RoofLabel(help, "HELP", 0.2f);
            BoardUp(help, rng);
            TornTape(help);

            var keepOut = Body(4, true);
            Label(keepOut, "KEEP\nOUT", new Vector3(7.2f, 1.75f, paint), Vector3.back, 0.12f, "SignRed");
            BoardUp(keepOut, rng);
        }

        // X sprayed by the search teams: team on the left, date on top, victims found at the bottom.
        static Mesh SearchMarkMesh(string victims) => Cached($"SM_Marque_Fouille_{victims}", () =>
        {
            var m = new MeshBuilder().Sub();
            foreach (var angle in new[] { 45f, -45f })
            {
                var rotation = Quaternion.Euler(0f, 0f, angle);
                Vector3 C(float x, float y) => rotation * new Vector3(x, y, 0f);
                m.Quad(C(-0.6f, -0.035f), C(0.6f, -0.035f), C(0.6f, 0.035f), C(-0.6f, 0.035f), Vector3.back);
            }
            AddText(m, "9 12", 0.025f, new Vector3(0f, 0.26f, 0f));
            AddText(m, "TF1", 0.025f, new Vector3(-0.27f, 0f, 0f));
            AddText(m, victims, 0.025f, new Vector3(0f, -0.27f, 0f));
            return m.Save($"SM_Marque_Fouille_{victims}");
        });

        // Painted halfway up the front slope of a house roof, above the porch, clear of the shingle courses.
        static void RoofLabel(Transform body, string text, float cell)
        {
            var hz = HouseRoofSpan / 2f;
            var normal = new Vector3(0f, hz, -HouseRoofHeight).normalized;
            var upSlope = new Vector3(0f, HouseRoofHeight, hz).normalized;
            var center = new Vector3(6f, WallHeight + HouseRoofHeight / 2f, 5f - hz / 2f) + normal * 0.05f;
            MeshObject("Inscription_Toit", body, TextMesh(text, cell), center, Quaternion.LookRotation(-normal, upSlope), Vector3.one, new[] { "SignWhite" }, false);
        }

        // Planks across, or a plywood sheet over, the side and back windows (House boards the front ones), standing
        // proud of the trims and shutters.
        static void BoardUp(Transform body, System.Random rng)
        {
            foreach (var wall in HouseShell.Skip(1))
            {
                var dir = (wall.To - wall.From).normalized;
                var outward = new Vector3(dir.y, 0f, -dir.x);
                var facing = Quaternion.LookRotation(outward);
                foreach (var o in wall.Openings.Where(o => o.Bottom > 0f))
                {
                    var at = new Vector3(wall.From.x + dir.x * o.Offset, 0f, wall.From.y + dir.y * o.Offset) + outward * (WallThickness / 2f);
                    if (rng.NextDouble() < 0.35)
                    {
                        Box("Contreplaque", body, at + outward * 0.08f + Vector3.up * 1.51f, new Vector3(1.4f, 1.22f, 0.025f), "Crate", facing, solid: false);
                        continue;
                    }
                    for (var k = 0; k < 3; k++)
                        Box("Planche", body, at + outward * 0.07f + Vector3.up * (1.15f + k * 0.35f), new Vector3(1.5f, 0.16f, 0.04f), "WoodDark",
                            facing * Quaternion.Euler(0f, 0f, k == 1 ? -12f : 9f), solid: false);
                }
            }
        }

        // Tape torn across the porch entry: each end still tied to a post behind the railing, one loose end on the
        // porch floor.
        static void TornTape(Transform body)
        {
            void Strip(params Vector3[] points)
            {
                for (var i = 0; i < points.Length - 1; i++)
                {
                    var (a, b) = (points[i], points[i + 1]);
                    var lying = Mathf.Abs(b.y - a.y) < 0.02f;
                    Box("Ruban", body, (a + b) / 2f, new Vector3(0.006f, 0.05f, Vector3.Distance(a, b)), "SignYellow",
                        Quaternion.LookRotation(b - a) * Quaternion.Euler(0f, 0f, lying ? 90f : 0f), solid: false);
                }
            }
            Strip(new Vector3(3.47f, 1f, -2.4f), new Vector3(4.3f, 0.95f, -2.4f), new Vector3(4.45f, 0.21f, -2.35f), new Vector3(4.8f, 0.205f, -2.1f));
            Strip(new Vector3(6.53f, 1f, -2.4f), new Vector3(5.75f, 0.93f, -2.4f), new Vector3(5.6f, 0.45f, -2.38f));
        }
    }
}
