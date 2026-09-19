using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru;

internal static class FruSequenceChecks
{
    public static void Run()
    {
        var catalog = FruAllScenario.CreateCatalog();
        var all = (IScenarioSequence)catalog[0];
        string[] names = ["Diamond Dust", "Light Rampant", "Ultimate Relativity", "Apocalypse", "Darklit Dragonsong", "Crystallize Time", "Fulgent Blade", "Paradise Regained"];
        float[] ends = [54,48,61,57,63,67,43.5f,54];
        Check(all.Name == "All" && all.Scenarios.Select(s=>s.Name).SequenceEqual(names), "All uses the complete FRU progression beginning with P2");
        Check(catalog.Skip(1).SequenceEqual(all.Scenarios), "All and individual entries share the same instances/settings");
        Check(all.Scenarios.Select(s=>s.Duration).SequenceEqual(ends), "Declared cleanup times cover the whole mechanic, including Polarizing Strikes");
        for (var i=0;i<8;i++) {
            var expectedPhase = i<2 ? FruZone.P2 : i<4 ? FruZone.P3 : i<6 ? FruZone.P4 : FruZone.P5;
            Check(all.Scenarios[i].Phase == expectedPhase, "Phase order");
            var strats = all.Scenarios[i].AiStrats;
            Check(strats[ScenarioSequence.AiIndex(all.Scenarios[i])].Group == "NA" || strats.Count == 1,
                "Select NA where available, otherwise the scenario's standard AI");
        }
        foreach (var fps in new[] {15,30,60,144}) foreach (var scale in new[] {.5f,1f,3f}) {
            var run = new ScenarioSequence(all.Scenarios);
            for (var i=0;i<8;i++) {
                var s=run.Current;
                Check(run.Index==i && run.Tick(s.Duration-.001f,1f/fps,false)==null&&!run.Waiting, "No early transition");
                // Even a large last frame starts a fresh two-second gap.
                Check(run.Tick(s.Duration+scale,1f/fps,false)==null, "Cleanup frame does not consume the gap");
                if(i==7) {Check(run.Finished&&!run.Waiting,"Final scenario finishes without looping"); break;}
                var elapsed=0f; IScenario? next=null;
                while(next==null) {
                    elapsed+=1f/fps;
                    next=run.Tick(s.Duration+elapsed*scale,1f/fps,false);
                    if(next==null) Check(elapsed<2.01f,"Transition eventually happens");
                    else Check(elapsed>=1.999f&&elapsed<=2+1f/fps+.001f&&next==all.Scenarios[i+1],"Two real seconds between consecutive scenarios");
                }
            }
            Check(run.Tick(999,999,false)==null,"Completed sequence stays complete");
        }
        foreach(var duringGap in new[]{false,true}) {
            var run=new ScenarioSequence(all.Scenarios);
            if(duringGap) run.Tick(run.Current.Duration,0,false);
            Check(run.Tick(run.Current.Duration,2,true)==null&&run.Finished,"A wipe cancels advancement, including in the gap");
            Check(run.Tick(999,999,false)==null,"Cancelled sequence cannot restart itself");
        }
        foreach(var bad in new[]{0f,-1,float.NaN,float.PositiveInfinity}) {
            try { _=new ScenarioSequence([new TestScenario(bad)]); throw new Exception("Accepted invalid duration"); }
            catch(ArgumentException) { }
        }
        var aiOrder = new TestScenario(1);
        Check(ScenarioSequence.AiIndex(aiOrder)==1,"NA need not be the first AI entry");
        var world=new SimWorld();
        var fulgent=catalog.Single(s=>s.Name=="Fulgent Blade");
        fulgent.Run(world,null);
        world.Events.Tick(7.49f);
        Check(!world.Spawned.OfType<SimOmen>().Any(o=>o.Path==FruConstants.Vfx.InitialSeam),"No exawave seams before cast completion");
        world.Events.Tick(.02f);
        var seams=world.Spawned.OfType<SimOmen>().Where(o=>o.Path==FruConstants.Vfx.InitialSeam).ToArray();
        Check(seams.Length==6&&seams.All(o=>o.StartTrigger==FruConstants.Vfx.InitialSeamTrigger),"All seams start as Fulgent Blade resolves");
        world.Events.Clear();world.Despawn();
        Check(seams.All(o=>!o.IsActive),"Seams clear on reset");
        Console.WriteLine("PASS: FRU All catalog/order/settings, eight cleanup durations, NA selection, two-second transitions at four frame rates/three event speeds, wipe cancellation and Fulgent seam timing.");
    }
    private sealed class TestScenario(float duration) : IScenario {
        public string Name=>"Test";public IPhase Phase=>FruZone.P2;public float Duration=>duration;
        public IReadOnlyList<IScenarioAi> AiStrats { get; }=[new TestAi("EU"),new TestAi("NA")];
        public void Run(SimWorld world,int? selectedAi) { }
    }
    private sealed class TestAi(string group) : IScenarioAi {public string Name=>group;public string Group=>group;}
    private static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
}
