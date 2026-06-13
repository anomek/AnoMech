using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Helpers;

public static unsafe class CharacterManagerHelper
{
    public static bool CreateCharacter(out int characterIndex, out Character* characterPtr, int preferredIndex = -1, uint? entityId = null, uint? layoutId = null)
    {
        var characterManager = CharacterManager.Instance();

        if (characterManager == null)
        {
            Plugin.Log.Warning("[BattleCharaSpawn.CreateCharacter] CharacterManager.Instance() was null.");
        }

        if (preferredIndex == -1)
        {
            for (int i = 0; i < characterManager->BattleCharas.Length; i++)
            {
                if (characterManager->BattleCharas[i] == null)
                {
                    preferredIndex = i;
                    break;
                }
            }

            if (preferredIndex == -1)
            {
                Plugin.Log.Warning("[BattleCharaSpawn.CreateCharacter] Unable to find a free index.");
                characterPtr = null;
                characterIndex = -1;
                return false;
            }
        }

        entityId ??= 0xE0000000 + (uint)preferredIndex;
        layoutId ??= 0;

        characterPtr = CharacterManagerPointers.CreateCharacterAtIndex(characterManager, entityId.Value, preferredIndex, layoutId.Value);
        characterIndex = preferredIndex;
        return characterPtr != null;
    }
}
