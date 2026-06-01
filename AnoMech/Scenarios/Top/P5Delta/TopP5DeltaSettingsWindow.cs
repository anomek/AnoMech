using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P5Delta;

public sealed class TopP5DeltaSettingsWindow
{
    public TopP5DeltaStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##p5delta"))
        {
#if DEBUG
            DrawEyeSpawn();
            DrawSwivelCannon();
#endif
            DrawTetherAssignment();

            var closeOnly = Overrides.TetherAssignment is
                PlayerTetherAssignment.FarAny or
                PlayerTetherAssignment.FarInner or
                PlayerTetherAssignment.FarOuter;
            var bdOnly = closeOnly || Overrides.TetherAssignment == PlayerTetherAssignment.CloseOuter;

            if (closeOnly) ImGui.BeginDisabled();
            DrawMonitor();
            DrawHelloWorld();
            if (closeOnly) ImGui.EndDisabled();

            if (bdOnly) ImGui.BeginDisabled();
            DrawBeyondDefence();
            if (bdOnly) ImGui.EndDisabled();

            SettingsGrid.End();
        }
    }

    private void ResetAll()
    {
#if DEBUG
        Overrides.EyeSpawn = null;
        Overrides.SwivelCannonSide = null;
#endif
        Overrides.TetherAssignment = PlayerTetherAssignment.Auto;
        Overrides.Monitor = null;
        Overrides.HelloWorld = HelloWorldOption.Auto;
        Overrides.BeyondDefence = null;
    }

#if DEBUG
    private void DrawEyeSpawn()
    {
        var mode = 0;
        if (Overrides.EyeSpawn == NorthSouth.North) mode = 1;
        if (Overrides.EyeSpawn == NorthSouth.South) mode = 2;
        SettingsGrid.Row("Eye spawn:");
        if (ImGui.RadioButton("Auto##eye",  mode == 0)) Overrides.EyeSpawn = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("North##eye", mode == 1)) Overrides.EyeSpawn = NorthSouth.North;
        ImGui.SameLine();
        if (ImGui.RadioButton("South##eye", mode == 2)) Overrides.EyeSpawn = NorthSouth.South;
    }

    private void DrawSwivelCannon()
    {
        var mode = Overrides.SwivelCannonSide == null ? 0
            : Overrides.SwivelCannonSide == Side.Left ? 1
            : 2;
        SettingsGrid.Row("Swivel Cannon:");
        if (ImGui.RadioButton("Auto##swivel",  mode == 0)) Overrides.SwivelCannonSide = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Left##swivel",  mode == 1)) Overrides.SwivelCannonSide = Side.Left;
        ImGui.SameLine();
        if (ImGui.RadioButton("Right##swivel", mode == 2)) Overrides.SwivelCannonSide = Side.Right;
    }
#endif

    private void DrawTetherAssignment()
    {
        var t = Overrides.TetherAssignment;
        SettingsGrid.Row("Tether:");
        if (ImGui.RadioButton("Auto##tether",        t == PlayerTetherAssignment.Auto))       Overrides.TetherAssignment = PlayerTetherAssignment.Auto;
        ImGui.SameLine();
        if (ImGui.RadioButton("Close any##tether",   t == PlayerTetherAssignment.CloseAny))   Overrides.TetherAssignment = PlayerTetherAssignment.CloseAny;
        ImGui.SameLine();
        if (ImGui.RadioButton("Close inner##tether", t == PlayerTetherAssignment.CloseInner)) Overrides.TetherAssignment = PlayerTetherAssignment.CloseInner;
        ImGui.SameLine();
        if (ImGui.RadioButton("Close outer##tether", t == PlayerTetherAssignment.CloseOuter)) Overrides.TetherAssignment = PlayerTetherAssignment.CloseOuter;
        // Second row: drop the leading SameLine so the Far options wrap within the cell.
        if (ImGui.RadioButton("Far any##tether",     t == PlayerTetherAssignment.FarAny))     Overrides.TetherAssignment = PlayerTetherAssignment.FarAny;
        ImGui.SameLine();
        if (ImGui.RadioButton("Far inner##tether",   t == PlayerTetherAssignment.FarInner))   Overrides.TetherAssignment = PlayerTetherAssignment.FarInner;
        ImGui.SameLine();
        if (ImGui.RadioButton("Far outer##tether",   t == PlayerTetherAssignment.FarOuter))   Overrides.TetherAssignment = PlayerTetherAssignment.FarOuter;
    }

    private void DrawMonitor()
    {
        var m = Overrides.Monitor;
        SettingsGrid.Row("Monitor:");
        if (ImGui.RadioButton("Auto##mon", m == null))  Overrides.Monitor = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##mon",  m == true))  Overrides.Monitor = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("No##mon",   m == false)) Overrides.Monitor = false;
    }

    private void DrawHelloWorld()
    {
        var h = Overrides.HelloWorld;
        SettingsGrid.Row("Hello World:");
        if (ImGui.RadioButton("Auto##hw", h == HelloWorldOption.Auto)) Overrides.HelloWorld = HelloWorldOption.Auto;
        ImGui.SameLine();
        if (ImGui.RadioButton("Near##hw", h == HelloWorldOption.Near)) Overrides.HelloWorld = HelloWorldOption.Near;
        ImGui.SameLine();
        if (ImGui.RadioButton("Far##hw",  h == HelloWorldOption.Far))  Overrides.HelloWorld = HelloWorldOption.Far;
        ImGui.SameLine();
        if (ImGui.RadioButton("No##hw",   h == HelloWorldOption.No))   Overrides.HelloWorld = HelloWorldOption.No;
    }

    private void DrawBeyondDefence()
    {
        var b = Overrides.BeyondDefence;
        SettingsGrid.Row("Beyond Defence:");
        if (ImGui.RadioButton("Auto##bd", b == null))  Overrides.BeyondDefence = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##bd",  b == true))  Overrides.BeyondDefence = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("No##bd",   b == false)) Overrides.BeyondDefence = false;
    }
}
