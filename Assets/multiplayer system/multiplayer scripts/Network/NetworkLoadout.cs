using System;
using Unity.Netcode;

namespace HitBoss.Multiplayer
{
    public struct NetworkLoadout : INetworkSerializable, IEquatable<NetworkLoadout>
    {
        public bool initialized;
        public int head, body, bag, skates;
        public static NetworkLoadout Capture(PlayerCustomizationManager source) => new NetworkLoadout
        {
            initialized = true,
            head = source.GetEquippedIndex(PlayerCustomizationManager.Category.Head),
            body = source.GetEquippedIndex(PlayerCustomizationManager.Category.Body),
            bag = source.GetEquippedIndex(PlayerCustomizationManager.Category.Bag),
            skates = source.GetEquippedIndex(PlayerCustomizationManager.Category.Skates)
        };
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref initialized); serializer.SerializeValue(ref head);
            serializer.SerializeValue(ref body); serializer.SerializeValue(ref bag); serializer.SerializeValue(ref skates);
        }
        public bool Equals(NetworkLoadout other) => initialized == other.initialized && head == other.head && body == other.body && bag == other.bag && skates == other.skates;
    }
}
