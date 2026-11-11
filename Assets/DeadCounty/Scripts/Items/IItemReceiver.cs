using System.Collections.Generic;

namespace DeadCounty.Items
{
    // Whoever can take items found in the world: the player's inventory.
    public interface IItemReceiver
    {
        void Receive(IReadOnlyList<ItemStack> items);
    }
}
