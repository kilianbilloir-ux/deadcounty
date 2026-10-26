namespace DeadCounty.Items
{
    // Whoever can take items found in the world: the player's inventory.
    public interface IItemReceiver
    {
        void Receive(ItemDefinition item, int count);
    }
}
