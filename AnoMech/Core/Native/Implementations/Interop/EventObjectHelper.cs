using AnoMech.Core.Native.Implementations.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace AnoMech.Core.Native.Implementations.Interop;

internal static unsafe partial class EventObjectHelper
{
    private const uint DefaultEntityIdBase = 0x4000FD00u;

    public static bool Create(SpawnObjectPacket* packet, out int slot, out GameObject* eventObject)
    {
        var manager = EventObjectManager.Instance();

        // Since the Packet Handle method does not return the ID, we do a manual check beforehand, and test if that ID was created.
        if (packet->ObjectIndex == 0xFF) // -1
        {
            var freeId = -1;

            for (int i = 0; i < 40; i++)
            {
                if (manager->EventObjects[i] == null)
                {
                    freeId = i;
                    break;
                }
            }

            if (freeId == -1)
            {
                slot = -1;
                eventObject = null;
                return false;
            }

            packet->ObjectIndex = (byte)freeId;
        }

        // ActorControls reach an EObj by entity id; EObjs sharing one (0, the default) get each
        // other's beats and DirectorEObjMods.
        if (packet->EntityId == 0)
            packet->EntityId = DefaultEntityIdBase + packet->ObjectIndex;

        PacketDispatcher.HandleSpawnObjectPacket(0, packet);
        slot = packet->ObjectIndex;
        eventObject = EventObjectManagerPointers.GetEventObjectByIndex(manager, (uint)slot);
        return eventObject != null;
    }
}
