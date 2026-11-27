// TestDriver: a test-only addon that plays a scripted scenario (named steps from a config file), checks
// things with ASSERT lines and ends the log with "[TestDriver] RESULT PASS", "RESULT FAIL ..." or "RESULT INCONCLUSIVE ...".
// Never ship it. Copy it, rename it, add the steps your mod needs (see Exec).
//
// Reads GameData/zzTestDriver/scenario.cfg (node TESTDRIVER); see scenario.cfg next to this file.
//
// What it does for you, each one learnt the hard way (docs/headless-lessons.md):
//  - the runner lives on a DontDestroyOnLoad object, so a scene change does not kill the scenario
//    (a coroutine on a scene's addon dies with the scene, silently);
//  - a step that throws is logged with its stack and counted as a failure; the scenario goes on;
//  - a background thread writes zzTestDriver/PluginData/heartbeat.txt (frames, coroutine ticks, memory,
//    scene, step), so "the log stopped" can be told apart from "the game hung" (frames still going up);
//  - a watchdog ends the round with RESULT FAIL when the time is up, instead of leaving KSP open;
//  - PASS needs proof: at least one expect (or Prove(...) in a step of your own) that passed. A round with no
//    failure and no such check ends INCONCLUSIVE, not PASS (a test that tested nothing is not green);
//  - the result is also written to zzTestDriver/PluginData/result.txt (one word: PASS, FAIL or INCONCLUSIVE).
//
// Steps (STEP { do = ... }); all log lines start with [TestDriver]:
//   wait    seconds = n
//   log     text = ...
//   open    scene = SPACECENTER | SPH | VAB | TRACKSTATION | MAINMENU      go there and wait until it is loaded
//   cycle   scenes = SPH,SPACECENTER,TRACKSTATION                          open each in turn (settle seconds after each)
//   var     key = name  set = value                                        a variable of your own
//   var     key = name  from = funds | reputation | science | scene | vessels | kerbals | situation | active-vessel
//   var     key = name  from = type  of = Namespace.TypeName               "1" when some loaded assembly has the type, else "0"
//   var     key = name  from = static  of = Namespace.Type.Member          a static field, property or no-argument method, as text
//   expect  key = name  value = x  [op = eq | ne | ge | le | contains]     ASSERT PASS / ASSERT FAIL (ge, le are numbers)
//   save                                                                   save the game now
//   shot    name = label                                                   Screenshots/testdriver-<name>-<label>.png
//   bounce  scenes = SPACECENTER,TRACKSTATION  times = 10  settle = 3       go back and forth with a short settle
// State checks (each one is a proof, like expect):
//   expect  state = antenna-extended                                       every deployable antenna of the active vessel is extended
//   expect  state = kerbal-in-roster  name = Jebediah Kerman | $variable  [status = Available]
//   expect  state = crew-active                                            the active vessel has crew and none of it is inactive (vacation)
//   expect  state = parachute-armed                                        the active vessel has parachutes, none cut or destroyed
//   expect  state = docking-port-free                                      the active vessel has a docking port in state Ready
//   expect  state = vessels  value = 1                                     number of vessels with a docking port in physics range (see dock)
//   expect  state = commnet-connected  [value = True]                      CommNet connection of the active vessel
//   expect  var = name=value                                               same as key = name value = value
// Flight (see README, "Flight steps"):
//   launch  craft = file.craft | /abs/path.craft  [site = LaunchPad]  [crew = Name Kerman]
//           crew: only Available Kerbals that are not inactive (CrewRandR vacation) go aboard; var crew = the names
//   focus   vessel = name                                                  open the flight scene on that vessel of the save
//   fly     seconds = 60  [throttle = 1]  [stage = true]                    full throttle, next stage, log the flight every 10 s
//   land    [seconds = 900]  [chutes = true]  [warp = 0..3]                  wait for LANDED or SPLASHED coming from FLYING or
//                                                                          SUB_ORBITAL; deploys the parachutes (they open when safe)
//   dock    [target = vessel name]  [gap = 0.3]  [seconds = 30]  [direct = false]
//           puts the active vessel's free port facing the target's free port, gap metres apart, and waits for the docking;
//           direct = true calls DockToVessel when the ports did not catch. Proof: onPartCouple or onDockingComplete
//           fired and both vessels are one
//   recover [vessel = name]  [seconds = 120]                                recovers the active (or named) vessel; proof:
//           onVesselRecovered for it
//   eva     [name = Kerbal name]                                           that crew member (or the first) leaves the active
//           vessel; proof: the active vessel is now that Kerbal on EVA
//   force   of = Namespace.Type.Member  [value = x]  [hold = true]          set a static field or property and read it back;
//           no value = True held on every frame (keep a window open across scenes)
//   force   scenario = ScenarioModuleName  field = name  value = x         set a field of a ScenarioModule and read it back
//   commnet-diag                                                           every CommNetHome (name, lat/lon, distance to the
//           active vessel, isKSC, origin) and the active vessel's CommNet state (IsConnected, SignalStrength, path)
// A step of your own: put it in a second file, "public partial class TestDriver { partial void AddSteps() { steps["zz"] = MyStep; } }"
// (IEnumerator MyStep(ConfigNode step)); build.sh compiles every .cs next to this one. Use Prove(...) for what the
// test is about, or set assertsProve = true while your step runs when all its checks are about your mod.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class TestDriverBoot : MonoBehaviour
{
    static bool started;

    void Start()
    {
        if (started) return;
        ConfigNode cfg = TestDriver.LoadScenario();
        if (cfg == null) { Debug.Log("[TestDriver] no scenario at " + TestDriver.ScenarioPath + "; nothing to do"); started = true; return; }
        started = true;
        var go = new GameObject("TestDriverRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<TestDriver>().Begin(cfg);
    }
}

public partial class TestDriver : MonoBehaviour
{
    public static string ScenarioPath { get { return Path.Combine(KSPUtil.ApplicationRootPath, "GameData/zzTestDriver/scenario.cfg"); } }
    static string DataDir { get { return Path.Combine(KSPUtil.ApplicationRootPath, "GameData/zzTestDriver/PluginData"); } }

    public static ConfigNode LoadScenario()
    {
        ConfigNode root = File.Exists(ScenarioPath) ? ConfigNode.Load(ScenarioPath) : null;
        return root == null ? null : root.GetNode("TESTDRIVER");
    }

    static void Log(string msg) { Debug.Log("[TestDriver] " + msg); }

    // shared with the heartbeat thread (plain fields: the thread only reads them)
    static volatile int frames, ticks;
    static volatile string scene = "?", stepLabel = "-";

    readonly Dictionary<string, string> vars = new Dictionary<string, string>();
    ConfigNode cfg;
    string scenarioName;
    // HighLogic.LoadedScene already says the target while the scene is still loading (seen: "reached the Space Center"
    // passed at once, three minutes before the scene was there). The reliable signal is this event.
    GameScenes readyScene = GameScenes.LOADING;
    void OnSceneReady(GameScenes s) { readyScene = s; Log("scene ready: " + s); }
    int checks, failed, proofs;   // proofs: passed checks that test the scenario (expect, Prove), not the plumbing
    protected bool assertsProve;  // while true, every passed Assert counts as a proof (a mod step whose checks are all about the mod)
    bool finished;
    // steps added by a copy, in its own file: partial void AddSteps() { steps["mystep"] = MyStep; }
    protected readonly Dictionary<string, Func<ConfigNode, IEnumerator>> steps = new Dictionary<string, Func<ConfigNode, IEnumerator>>();
    partial void AddSteps();

    public void Begin(ConfigNode c)
    {
        cfg = c;
        GameEvents.onLevelWasLoadedGUIReady.Add(OnSceneReady);   // an instance method: a static one has a null Target and throws
        scenarioName = c.GetValue("name") ?? "scenario";
        AddSteps();
        GameEvents.onPartCouple.Add(OnCouple);
        GameEvents.onDockingComplete.Add(OnDocked);
        GameEvents.onVesselRecovered.Add(OnRecovered);
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(Path.Combine(DataDir, "result.txt"), "RUNNING\n");
        var t = new Thread(BeatLoop);
        t.IsBackground = true;
        t.Start();
        StartCoroutine(Watchdog(Num(cfg, "timeout", 600)));
        StartCoroutine(Run());
    }

    // static fields or properties held at a value on every frame (force ... hold = true)
    readonly List<KeyValuePair<MemberInfo, object>> forced = new List<KeyValuePair<MemberInfo, object>>();
    void Update()
    {
        frames++; scene = HighLogic.LoadedScene.ToString();
        foreach (var f in forced) TdSet(f.Key, null, f.Value);
    }

    // events the flight steps prove with (instance methods: a static one has a null Target and throws in EventData.Add)
    int couples; string lastRecovered;
    void OnCouple(GameEvents.FromToAction<Part, Part> a) { couples++; Log("   event onPartCouple " + a.from.partInfo.name + " -> " + a.to.partInfo.name); }
    void OnDocked(GameEvents.FromToAction<Part, Part> a) { couples++; Log("   event onDockingComplete " + a.from.partInfo.name + " -> " + a.to.partInfo.name); }
    void OnRecovered(ProtoVessel pv, bool quick) { lastRecovered = pv.vesselName; Log("   event onVesselRecovered " + pv.vesselName); }

    // Heartbeat: a thread, so it keeps writing when the main thread (or only a coroutine) is stuck.
    void BeatLoop()
    {
        string file = Path.Combine(DataDir, "heartbeat.txt");
        while (true)
        {
            try
            {
                File.WriteAllText(file, string.Format("{0:HH:mm:ss} frames={1} coroutineTicks={2} memoryMB={3} scene={4} step={5}\n",
                    DateTime.Now, frames, ticks, GC.GetTotalMemory(false) / (1024 * 1024), scene, stepLabel));
            }
            catch (Exception) { }
            Thread.Sleep(5000);
        }
    }

    IEnumerator Watchdog(double seconds)
    {
        yield return new WaitForSecondsRealtime((float)seconds);
        if (finished) yield break;
        Log("ASSERT FAIL: scenario did not finish in " + seconds + " s (last step: " + stepLabel + ")");
        failed++; checks++;
        Finish();
    }

    // ---- helpers ----------------------------------------------------------------------------
    static double Num(ConfigNode n, string key, double def)
    {
        double v;
        return n.HasValue(key) && double.TryParse(n.GetValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def;
    }

    static string Fmt(double d) { return d.ToString("0.###", CultureInfo.InvariantCulture); }

    static Type FindType(string full)
    {
        foreach (var la in AssemblyLoader.loadedAssemblies)
        {
            try { Type t = la.assembly.GetType(full); if (t != null) return t; } catch (Exception) { }
        }
        return null;
    }

    string Read(ConfigNode step)
    {
        string from = step.GetValue("from"), of = step.GetValue("of");
        switch (from)
        {
            case "funds": return Funding.Instance == null ? "(none)" : Fmt(Funding.Instance.Funds);
            case "reputation": return Reputation.Instance == null ? "(none)" : Fmt(Reputation.Instance.reputation);
            case "science": return ResearchAndDevelopment.Instance == null ? "(none)" : Fmt(ResearchAndDevelopment.Instance.Science);
            case "scene": return HighLogic.LoadedScene.ToString();
            case "vessels": return FlightGlobals.Vessels == null ? "0" : FlightGlobals.Vessels.Count.ToString();
            case "situation": return FlightGlobals.ActiveVessel == null ? "(none)" : FlightGlobals.ActiveVessel.situation.ToString();
            case "active-vessel": return FlightGlobals.ActiveVessel == null ? "(none)" : FlightGlobals.ActiveVessel.vesselName;
            case "kerbals": return HighLogic.CurrentGame == null ? "0" : HighLogic.CurrentGame.CrewRoster.Count.ToString();
            case "type": return FindType(of) != null ? "1" : "0";
            case "static":
            {
                int dot = of.LastIndexOf('.');
                Type t = FindType(of.Substring(0, dot));
                if (t == null) return "(type not found)";
                string m = of.Substring(dot + 1);
                BindingFlags f = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo fi = t.GetField(m, f); if (fi != null) return Convert.ToString(fi.GetValue(null), CultureInfo.InvariantCulture);
                PropertyInfo pi = t.GetProperty(m, f); if (pi != null) return Convert.ToString(pi.GetValue(null, null), CultureInfo.InvariantCulture);
                MethodInfo mi = t.GetMethod(m, f, null, Type.EmptyTypes, null); if (mi != null) return Convert.ToString(mi.Invoke(null, null), CultureInfo.InvariantCulture);
                return "(member not found)";
            }
        }
        return "(unknown from = " + from + ")";
    }

    void Assert(string label, bool ok, string detail)
    {
        checks++;
        if (!ok) failed++;
        else if (assertsProve) proofs++;
        Log("ASSERT " + (ok ? "PASS" : "FAIL") + ": " + label + (detail.Length > 0 ? " (" + detail + ")" : ""));
    }

    // A check that proves something about the mod: counts towards PASS. Use it in your own steps instead of Assert
    // when the check is the point of the test (Assert alone is for plumbing: scene loaded, save loaded).
    void Prove(string label, bool ok, string detail)
    {
        bool was = assertsProve; assertsProve = false;
        Assert(label, ok, detail);
        assertsProve = was;
        if (ok) proofs++;
    }

    void Expect(ConfigNode step)
    {
        if (step.HasValue("state")) { ExpectState(step); return; }
        string key = step.GetValue("key"), want = step.GetValue("value") ?? "", op = step.GetValue("op") ?? "eq";
        if (step.HasValue("var"))   // var = name=value
        {
            string kv = step.GetValue("var"); int eq = kv.IndexOf('=');
            if (eq < 1) { Assert("expect var " + kv, false, "write var = name=value"); return; }
            key = kv.Substring(0, eq).Trim(); want = kv.Substring(eq + 1).Trim();
        }
        string got;
        if (!vars.TryGetValue(key, out got)) { Assert(key, false, "variable not set"); return; }
        bool ok;
        double a, b;
        switch (op)
        {
            case "ne": ok = got != want; break;
            case "contains": ok = got.Contains(want); break;
            case "ge": case "le":
                if (!double.TryParse(got, NumberStyles.Float, CultureInfo.InvariantCulture, out a) || !double.TryParse(want, NumberStyles.Float, CultureInfo.InvariantCulture, out b)) { Assert(key, false, "not numbers: " + got + " " + op + " " + want); return; }
                ok = op == "ge" ? a >= b : a <= b; break;
            default: ok = got == want; break;
        }
        Prove(key + " " + op + " " + want, ok, "got " + got);
    }

    // ---- the scenario -----------------------------------------------------------------------
    IEnumerator Run()
    {
        string save = cfg.GetValue("save");
        yield return Pause(3f);
        if (!string.IsNullOrEmpty(save))
        {
            Game game = GamePersistence.LoadGame("persistent", save, true, false);
            if (game == null) { Assert("load save " + save, false, "LoadGame returned null"); Finish(); yield break; }
            Log("loaded save " + save);
            game.startScene = GameScenes.SPACECENTER;
            HighLogic.CurrentGame = game;
            HighLogic.SaveFolder = save;
            readyScene = GameScenes.LOADING;
            game.Start();
            for (int t = 0; t < 300 && readyScene != GameScenes.SPACECENTER; t++) yield return Pause(1f);
            Assert("reached the Space Center", readyScene == GameScenes.SPACECENTER, "scene " + HighLogic.LoadedScene);
            yield return Pause((float)Num(cfg, "settle", 15));
        }
        int n = 0;
        foreach (ConfigNode step in cfg.GetNodes("STEP"))
        {
            if (finished) yield break;   // the watchdog ended the round
            string what = step.GetValue("do") ?? "?";
            stepLabel = ++n + ":" + what;
            Log("step " + stepLabel);
            // Nested IEnumerators (yield return OpenScene(...), a step of your own) are driven here, not handed to Unity:
            // Unity would run them as coroutines of their own, where an exception kills them silently and the step hangs.
            var stack = new Stack<IEnumerator>();
            stack.Push(Exec(what, step));
            while (stack.Count > 0 && !finished)
            {
                IEnumerator top = stack.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception e) { Assert("step " + stepLabel + " did not throw", false, e.ToString()); break; }
                if (!more) { stack.Pop(); continue; }
                ticks++;
                object cur = top.Current;
                if (cur is IEnumerator) { stack.Push((IEnumerator)cur); continue; }   // also CustomYieldInstruction (WaitForSecondsRealtime)
                yield return cur;
            }
            yield return Pause(1f);
        }
        stepLabel = "end";
        Finish();
    }

    object Pause(float s) { ticks++; return new WaitForSecondsRealtime(s); }

    IEnumerator Exec(string what, ConfigNode step)
    {
        switch (what)
        {
            case "wait":
                yield return new WaitForSecondsRealtime((float)Num(step, "seconds", 1));
                break;
            case "log":
                Log("   " + step.GetValue("text"));
                break;
            case "var":
            {
                string key = step.GetValue("key");
                vars[key] = step.HasValue("set") ? step.GetValue("set") : Read(step);
                Log("   " + key + " = " + vars[key]);
                break;
            }
            case "expect":
                Expect(step);
                break;
            case "save":
                GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                Log("   saved");
                break;
            case "shot":
                yield return new WaitForEndOfFrame();
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                string file = Path.Combine(KSPUtil.ApplicationRootPath, "Screenshots", "testdriver-" + scenarioName + "-" + (step.GetValue("name") ?? "shot") + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, shot.EncodeToPNG());
                Log("   screenshot " + file);
                break;
            case "open":
                yield return OpenScene(step.GetValue("scene"));
                break;
            case "cycle":
                foreach (string s in step.GetValue("scenes").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    yield return OpenScene(s.Trim());
                break;
            case "bounce":
            {
                int times = (int)Num(step, "times", 10);
                float st = (float)Num(step, "settle", 3);
                string[] two = step.GetValue("scenes").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < times; i++)
                    foreach (string s in two) { Log("   bounce " + (i + 1) + "/" + times); yield return OpenScene(s.Trim(), st); }
                break;
            }
            case "force": Force(step); break;
            case "commnet-diag": CommNetDiag(); break;
            case "launch": yield return Launch(step); break;
            case "focus": yield return Focus(step); break;
            case "fly": yield return Fly(step); break;
            case "land": yield return Land(step); break;
            case "dock": yield return Dock(step); break;
            case "recover": yield return Recover(step); break;
            case "eva": yield return Eva(step); break;
            default:
                Func<ConfigNode, IEnumerator> own;
                if (steps.TryGetValue(what, out own)) { yield return own(step); break; }
                Assert("known step '" + what + "'", false, "add it to Exec or to steps in AddSteps");
                break;
        }
    }

    IEnumerator OpenScene(string s) { return OpenScene(s, -1); }
    IEnumerator OpenScene(string s, float settle)
    {
        GameScenes target = s == "SPH" || s == "VAB" ? GameScenes.EDITOR : (GameScenes)Enum.Parse(typeof(GameScenes), s, true);
        Log("   -> " + s);
        readyScene = GameScenes.LOADING;
        if (s == "SPH") EditorDriver.StartEditor(EditorFacility.SPH);
        else if (s == "VAB") EditorDriver.StartEditor(EditorFacility.VAB);
        else HighLogic.LoadScene(target);
        for (int t = 0; t < 180 && readyScene != target; t++) yield return new WaitForSecondsRealtime(1f);
        Assert("scene " + s + " loaded", readyScene == target, "scene " + HighLogic.LoadedScene);
        yield return new WaitForSecondsRealtime(settle >= 0 ? settle : (float)Num(cfg, "settle", 15));   // let the scene's addons start
    }

    // The last line of the driver in the log is RESULT. KSP.log reaches the disk late: quit after a pause.
    void Finish()
    {
        if (finished) return;
        finished = true;
        string result = failed > 0 ? "FAIL" : proofs == 0 ? "INCONCLUSIVE" : "PASS";
        File.WriteAllText(Path.Combine(DataDir, "result.txt"), result + "\n");
        Log("RESULT " + result + " (" + (checks - failed) + " of " + checks + " checks passed, " + proofs + " expect passed)"
            + (result == "INCONCLUSIVE" ? ": nothing was proved; add an expect step" : ""));
        StartCoroutine(Quit());
    }

    IEnumerator Quit() { yield return new WaitForSecondsRealtime(3f); Application.Quit(); }
}
