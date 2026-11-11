using UnityEngine;

namespace DeadCounty.Core
{
    // Hands the wind to the foliage shader (Shaders/Wind.hlsl). Without it plants stand still.
    public class WindDirector : MonoBehaviour
    {
        static readonly int WindId = Shader.PropertyToID("_DcWind");

        // Real time: plants keep moving behind the title screen and do not thrash while sleep speeds the game up.
        void Update()
        {
            var clock = Time.unscaledTime;
            var wind = WindModel.Sample(clock);
            Shader.SetGlobalVector(WindId, new Vector4(wind.x, wind.y, wind.z, clock));
        }

        void OnDisable() => Shader.SetGlobalVector(WindId, Vector4.zero);
    }
}
