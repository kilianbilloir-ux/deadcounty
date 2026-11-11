using UnityEngine;

namespace DeadCounty.AI
{
    public enum ZombieLod { Near, Mid, Far, Dormant }

    // What the manager keeps of a zombie independently of its scene view: enough to reset, save or, later,
    // simulate it as plain data once hordes outnumber the active budget.
    public struct ZombieRecord
    {
        public Vector3 SpawnPosition;
        public Quaternion SpawnRotation;
        public int Seed;
        public ZombieProfile Profile;
        public bool Dead;
        // Null while no enabled ZombieAgent stands for this record.
        public ZombieAgent View;
    }
}
