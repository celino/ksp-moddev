// TestDriver: a test-only addon that plays a scripted scenario (named steps from a config file), checks
// things with ASSERT lines and ends the log with "[TestDriver] RESULT PASS" or "[TestDriver] RESULT FAIL ...".
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
//  - the result is also written to zzTestDriver/PluginData/result.txt (one word: PASS or FAIL).
//
// Steps (STEP { do = ... }); all log lines start with [TestDriver]:
//   wait    seconds = n
//   log     text = ...
//   open    scene = SPACECENTER | SPH | VAB | TRACKSTATION | MAINMENU      go there and wait until it is loaded
//   cycle   scenes = SPH,SPACECENTER,TRACKSTATION                          open each in turn (settle seconds after each)
//   var     key = name  set = value                                        a variable of your own
//   var     key = name  from = funds | reputation | science | scene | vessels | kerbals
//   var     key = name  from = type  of = Namespace.TypeName               "1" when some loaded assembly has the type, else "0"
//   var     key = name  from = static  of = Namespace.Type.Member          a static field, property or no-argument method, as text
//   expect  key = name  value = x  [op = eq | ne | ge | le | contains]     ASSERT PASS / ASSERT FAIL (ge, le are numbers)
//   save                                                                   save the game now
//   shot    name = label                                                   Screenshots/testdriver-<name>-<label>.png
// Add a "zz" step of your own in Exec for what your mod needs (read its state, call it, change it).
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

public class TestDriver : MonoBehaviour
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
    int checks, failed;
    bool finished;

    public void Begin(ConfigNode c)
    {
        cfg = c;
        GameEvents.onLevelWasLoadedGUIReady.Add(OnSceneReady);   // an instance method: a static one has a null Target and throws
        scenarioName = c.GetValue("name") ?? "scenario";
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(Path.Combine(DataDir, "result.txt"), "RUNNING\n");
        var t = new Thread(BeatLoop);
        t.IsBackground = true;
        t.Start();
        StartCoroutine(Watchdog(Num(cfg, "timeout", 600)));
        StartCoroutine(Run());
    }

    void Update() { frames++; scene = HighLogic.LoadedScene.ToString(); }

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
        Log("ASSERT " + (ok ? "PASS" : "FAIL") + ": " + label + (detail.Length > 0 ? " (" + detail + ")" : ""));
    }

    void Expect(ConfigNode step)
    {
        string key = step.GetValue("key"), want = step.GetValue("value") ?? "", op = step.GetValue("op") ?? "eq";
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
        Assert(key + " " + op + " " + want, ok, "got " + got);
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
            IEnumerator sub = Exec(what, step);
            while (true)
            {
                bool more;
                try { more = sub.MoveNext(); }
                catch (Exception e) { Assert("step " + stepLabel + " did not throw", false, e.ToString()); break; }
                if (!more) break;
                ticks++;
                yield return sub.Current;
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
            default:
                Assert("known step '" + what + "'", false, "add it to Exec");
                break;
        }
    }

    IEnumerator OpenScene(string s)
    {
        GameScenes target = s == "SPH" || s == "VAB" ? GameScenes.EDITOR : (GameScenes)Enum.Parse(typeof(GameScenes), s, true);
        Log("   -> " + s);
        readyScene = GameScenes.LOADING;
        if (s == "SPH") EditorDriver.StartEditor(EditorFacility.SPH);
        else if (s == "VAB") EditorDriver.StartEditor(EditorFacility.VAB);
        else HighLogic.LoadScene(target);
        for (int t = 0; t < 180 && readyScene != target; t++) yield return new WaitForSecondsRealtime(1f);
        Assert("scene " + s + " loaded", readyScene == target, "scene " + HighLogic.LoadedScene);
        yield return new WaitForSecondsRealtime((float)Num(cfg, "settle", 15));   // let the scene's addons start
    }

    // The last line of the driver in the log is RESULT. KSP.log reaches the disk late: quit after a pause.
    void Finish()
    {
        if (finished) return;
        finished = true;
        string result = failed == 0 ? "PASS" : "FAIL";
        File.WriteAllText(Path.Combine(DataDir, "result.txt"), result + "\n");
        Log("RESULT " + result + " (" + (checks - failed) + " of " + checks + " checks passed)");
        StartCoroutine(Quit());
    }

    IEnumerator Quit() { yield return new WaitForSecondsRealtime(3f); Application.Quit(); }
}
