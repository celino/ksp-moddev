// A test-only addon for moddev-headless: it drives the game the way a player would, logs what
// it sees with a [TestProbe] prefix and quits. Never ship it. Copy, rename, and change Run().
//
//   dotnet build -c Release
//   moddev-headless --save mysave --put out/TestProbe.dll=GameData/zzTestProbe/TestProbe.dll \
//                   --until '\[TestProbe\] done' --timeout 1800
//
// Headless, there is no GPU and no player: FlightGlobals.ActiveVessel can stay null even in
// flight, and everything takes longer. Wait generously and log what you find.
using System.Collections;
using UnityEngine;

[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class TestProbe : MonoBehaviour
{
    const string Save = "mysave";          // a folder in saves/, copied in with --save
    const string Vessel = "";              // vessel name to fly; empty = the first one

    static int flights;

    void Start()
    {
        DontDestroyOnLoad(this);
        GameEvents.onLevelWasLoadedGUIReady.Add(OnScene);
        StartCoroutine(Begin());
    }

    void OnDestroy()
    {
        GameEvents.onLevelWasLoadedGUIReady.Remove(OnScene);
    }

    static void Log(string msg) => Debug.Log("[TestProbe] " + msg);

    IEnumerator Begin()
    {
        yield return new WaitForSeconds(5f);
        Fly();
    }

    void Fly()
    {
        Game game = GamePersistence.LoadGame("persistent", Save, true, false);
        if (game == null) { Log("no save " + Save + "; done"); Application.Quit(); return; }
        HighLogic.SaveFolder = Save;
        int idx = string.IsNullOrEmpty(Vessel) ? 0
            : game.flightState.protoVessels.FindIndex(p => p.vesselName == Vessel);
        Log("loading flight, vessel index " + idx);
        FlightDriver.StartAndFocusVessel(game, idx);
    }

    void OnScene(GameScenes scene)
    {
        Log("scene " + scene);
        if (scene == GameScenes.FLIGHT)
        {
            flights++;
            StartCoroutine(InFlight());
        }
    }

    IEnumerator InFlight()
    {
        yield return new WaitForSeconds(15f);
        Vessel v = FlightGlobals.ActiveVessel ?? (FlightGlobals.Vessels.Count > 0 ? FlightGlobals.Vessels[0] : null);
        Log("flight " + flights + ", vessel " + (v != null ? v.vesselName : "none"));

        // Your test goes here: fire an event, call the mod's API, count objects, and log the result.
        try
        {
            Log("vessels loaded: " + FlightGlobals.Vessels.Count);
        }
        catch (System.Exception e)
        {
            Log("test threw " + e);
        }

        Log("done");
        yield return new WaitForSeconds(2f);
        Application.Quit();
    }
}
