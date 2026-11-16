using UnityEngine;

namespace DeadCounty.Audio
{
    // Says what a collider without a renderer of its own sounds like underfoot: the block around a model.
    [DisallowMultipleComponent]
    public class GroundSurfaceTag : MonoBehaviour
    {
        public Surface surface;
    }
}
