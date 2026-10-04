using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

public sealed class TopP3HelloWorldSettingsWindow
{
    public TopP3HelloWorldStateOverrides Overrides { get; } = new();

    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] ColorLabels = ["Random", "Red", "Blue"];
    private static readonly string[] RoleLabels = ["Random", "Defamation", "Blue tether", "Stack", "Christmas tether"];

    // Solo, the player's own pick sits in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.DefamationColor = null;
            if (solo) Overrides.Role.Mine = null;
        }

        if (SettingsGrid.Begin("##p3helloworld"))
        {
            SettingsGrid.Row("Defamation rot:");
            var color = Overrides.DefamationColor is { } rot ? (int)rot + 1 : 0;
            SettingsGrid.ItemWidth(140);
            if (ImGui.Combo("##hwcolor", ref color, ColorLabels, ColorLabels.Length))
                Overrides.DefamationColor = color == 0 ? null : (DefamationRot)(color - 1);

            if (solo) DrawPlayerRows();
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) Overrides.Role.Clear();
        if (SettingsGrid.Begin("##p3helloworldplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##hwseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("First roles set:", Overrides.Role);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var whose = PerRole.SeatsActive ? "" : "Your ";
        SettingsGrid.Row($"{whose}first patch:");
        var role = Overrides.Role.Effective(editingSeat) is { } r ? (int)r + 1 : 0;
        SettingsGrid.ItemWidth(160);
        if (ImGui.Combo("##hwrole", ref role, RoleLabels, RoleLabels.Length))
            Overrides.Role.Set(editingSeat, role == 0 ? null : (HelloWorldRole)(role - 1));
    }
}
