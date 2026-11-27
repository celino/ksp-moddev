// TestDriver, part 2: the generic flight and state steps (launch, fly, land, dock, recover, eva, force, commnet-diag,
// expect state = ...). Compiled together with TestDriver.cs (build.sh takes every .cs in this folder). See the README.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using KSP.Localization;
using UnityEngine;

public partial class TestDriver
{
    const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // ---- reflection helpers -----------------------------------------------------------------
    static object TdGet(MemberInfo m, object o)
    {
        var f = m as FieldInfo; if (f != null) return f.GetValue(o);
        var p = m as PropertyInfo; return p != null ? p.GetValue(o, null) : null;
    }

    static void TdSet(MemberInfo m, object o, object v)
    {
        var f = m as FieldInfo; if (f != null) { f.SetValue(o, v); return; }
        var p = m as PropertyInfo; if (p != null) p.SetValue(o, v, null);
    }

    static Type TdTypeOf(MemberInfo m) { var f = m as FieldInfo; return f != null ? f.FieldType : ((PropertyInfo)m).PropertyType; }

    static MemberInfo TdFind(Type t, string name, BindingFlags fl)
    {
        for (; t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, fl | BindingFlags.DeclaredOnly); if (f != null) return f;
            PropertyInfo p = t.GetProperty(name, fl | BindingFlags.DeclaredOnly); if (p != null && p.CanWrite) return p;
        }
        return null;
    }

    static object TdParse(string text, Type t)
    {
        if (t == typeof(string)) return text;
        if (t.IsEnum) return Enum.Parse(t, text, true);
        return Convert.ChangeType(text, t, CultureInfo.InvariantCulture);
    }

    static string TdText(object o) { return Convert.ToString(o, CultureInfo.InvariantCulture); }

    // ---- force: set a value and read it back ----------------------------------------------------
    void Force(ConfigNode step)
    {
        string value = step.GetValue("value");
        if (step.HasValue("scenario"))
        {
            string sname = step.GetValue("scenario"), field = step.GetValue("field");
            ScenarioModule sm = null;
            if (HighLogic.CurrentGame != null)
                foreach (ProtoScenarioModule psm in HighLogic.CurrentGame.scenarios)
                    if (psm.moduleName == sname && psm.moduleRef != null) sm = psm.moduleRef;
            if (sm == null) { Assert("force scenario " + sname, false, "not loaded in scene " + HighLogic.LoadedScene); return; }
            MemberInfo m = TdFind(sm.GetType(), field, AnyInstance);
            if (m == null) { Assert("force " + sname + "." + field, false, "no such field or settable property"); return; }
            TdSet(m, sm, TdParse(value, TdTypeOf(m)));
            string back = TdText(TdGet(m, sm));
            Assert("force " + sname + "." + field + " = " + value, back == TdText(TdParse(value, TdTypeOf(m))), "read back " + back);
            vars[step.GetValue("key") ?? sname + "." + field] = back;
            return;
        }
        string of = step.GetValue("of") ?? "";
        int dot = of.LastIndexOf('.');
        Type t = dot > 0 ? FindType(of.Substring(0, dot)) : null;
        MemberInfo mi = t == null ? null : TdFind(t, of.Substring(dot + 1), AnyStatic);
        if (mi == null) { Assert("force " + of, false, t == null ? "type not found" : "no static field or settable property"); return; }
        object v = TdParse(value ?? "True", TdTypeOf(mi));
        TdSet(mi, null, v);
        string got = TdText(TdGet(mi, null));
        Assert("force " + of + " = " + TdText(v), got == TdText(v), "read back " + got);
        vars[step.GetValue("key") ?? of] = got;
        bool hold = step.HasValue("hold") ? step.GetValue("hold").ToLowerInvariant() == "true" : value == null;
        if (hold) forced.Add(new KeyValuePair<MemberInfo, object>(mi, v));
    }

    // ---- expect state = ... -----------------------------------------------------------------------
    void ExpectState(ConfigNode step)
    {
        string what = step.GetValue("state");
        Vessel v = FlightGlobals.ActiveVessel;
        switch (what)
        {
            case "antenna-extended":
            {
                var ants = v == null ? new List<ModuleDeployableAntenna>() : v.FindPartModulesImplementing<ModuleDeployableAntenna>();
                string st = string.Join(",", ants.Select(a => a.deployState.ToString()).ToArray());
                Prove("antenna-extended", ants.Count > 0 && ants.All(a => a.deployState == ModuleDeployablePart.DeployState.EXTENDED),
                    v == null ? "no active vessel" : ants.Count + " deployable antennas [" + st + "]");
                break;
            }
            case "kerbal-in-roster":
            {
                string name = step.GetValue("name") ?? "";
                if (name.StartsWith("$") && !vars.TryGetValue(name.Substring(1), out name)) name = "(variable " + step.GetValue("name") + " not set)";
                ProtoCrewMember p = HighLogic.CurrentGame == null ? null : HighLogic.CurrentGame.CrewRoster[name];
                string want = step.GetValue("status");
                bool ok = p != null && (want == null || p.rosterStatus.ToString() == want);
                Prove("kerbal-in-roster " + name + (want != null ? " " + want : ""), ok,
                    p == null ? "not in the roster" : "status " + p.rosterStatus + " type " + p.type + " inactive " + p.inactive);
                break;
            }
            case "crew-active":
            {
                var crew = v == null ? new List<ProtoCrewMember>() : v.GetVesselCrew();
                Prove("crew-active", crew.Count > 0 && crew.All(c => !c.inactive),
                    string.Join(", ", crew.Select(c => c.name + (c.inactive ? " (inactive)" : "")).ToArray()));
                break;
            }
            case "parachute-armed":
            {
                var chutes = v == null ? new List<ModuleParachute>() : v.FindPartModulesImplementing<ModuleParachute>();
                Prove("parachute-armed", chutes.Count > 0 && chutes.All(c => c.deploymentState != ModuleParachute.deploymentStates.CUT),
                    chutes.Count + " parachutes [" + string.Join(",", chutes.Select(c => c.deploymentState.ToString()).ToArray()) + "]");
                break;
            }
            case "docking-port-free":
            {
                var ports = v == null ? new List<ModuleDockingNode>() : v.FindPartModulesImplementing<ModuleDockingNode>();
                Prove("docking-port-free", ports.Any(PortFree),
                    ports.Count + " ports [" + string.Join(",", ports.Select(d => d.state).ToArray()) + "]");
                break;
            }
            case "vessels":
            {
                int n = FlightGlobals.VesselsLoaded == null ? 0 : FlightGlobals.VesselsLoaded.Count(x => x.FindPartModulesImplementing<ModuleDockingNode>().Count > 0);
                Prove("vessels with a docking port in range = " + step.GetValue("value"), n.ToString() == step.GetValue("value"), "got " + n);
                break;
            }
            case "commnet-connected":
            {
                string want = step.GetValue("value") ?? "True";
                string got = v == null || v.connection == null ? "(no connection)" : v.connection.IsConnected.ToString();
                Prove("commnet-connected " + want, got == want, "got " + got);
                break;
            }
            default:
                Assert("known expect state '" + what + "'", false, "see the list at the top of TestDriver.cs");
                break;
        }
    }

    static bool PortFree(ModuleDockingNode d) { return d.state == "Ready" && d.otherNode == null; }

    // ---- commnet-diag -----------------------------------------------------------------------------
    void CommNetDiag()
    {
        Vessel v = FlightGlobals.ActiveVessel;
        Vector3d vpos = v == null ? Vector3d.zero : v.GetWorldPos3D();
        var homes = UnityEngine.Object.FindObjectsOfType<CommNet.CommNetHome>();
        Log("   commnet: " + homes.Length + " CommNetHome objects; CommNet enabled = "
            + (HighLogic.CurrentGame == null ? "?" : HighLogic.CurrentGame.Parameters.Difficulty.EnableCommNet.ToString()));
        foreach (CommNet.CommNetHome h in homes)
        {
            CelestialBody body = (CelestialBody)TdGet(TdFind(typeof(CommNet.CommNetHome), "body", AnyInstance), h);
            Vector3d pos = h.nodeTransform != null ? (Vector3d)h.nodeTransform.position : (Vector3d)h.transform.position;
            string latlon = body == null ? "body ?" : body.name + " lat " + Fmt(body.GetLatitude(pos)) + " lon " + Fmt(body.GetLongitude(pos));
            string origin = h.GetType().FullName + " in " + h.GetType().Assembly.GetName().Name + ", object " + h.gameObject.name
                + (h.transform.parent != null ? " under " + h.transform.parent.name : "");
            Log("   home " + h.nodeName + " (" + h.displaynodeName + "): " + latlon + ", distance "
                + (v == null ? "-" : Fmt(Vector3d.Distance(pos, vpos) / 1000) + " km") + ", isKSC " + h.isKSC + ", power " + Fmt(h.antennaPower)
                + ", enabled " + h.enabled + ", " + origin);
        }
        if (v == null) { Log("   commnet: no active vessel"); return; }
        CommNet.CommNetVessel c = v.connection;
        if (c == null) { Log("   commnet: " + v.vesselName + " has no CommNetVessel"); return; }
        string path = c.ControlPath == null ? "(none)" : c.ControlPath.Count + " links: "
            + string.Join(" -> ", c.ControlPath.Select(l => l.a.displayName + "=>" + l.b.displayName).ToArray());
        Log("   commnet vessel " + v.vesselName + ": IsConnected " + c.IsConnected + ", IsConnectedHome " + c.IsConnectedHome
            + ", SignalStrength " + Fmt(c.SignalStrength) + ", ControlState " + c.ControlState + ", ControlPath " + path);
        vars["commnet.connected"] = c.IsConnected.ToString();
        vars["commnet.signal"] = Fmt(c.SignalStrength);
        vars["commnet.homes"] = homes.Length.ToString();
    }

    // ---- launch -------------------------------------------------------------------------------------
    // Only Available Kerbals that are not inactive go aboard: a Kerbal on CrewRandR vacation (inactive = True) leaves the
    // vessel without control (seen 27/11/2026: state Kerbal, CommNet not connected).
    IEnumerator Launch(ConfigNode step)
    {
        string c = step.GetValue("craft") ?? "";
        string craft = Path.IsPathRooted(c) ? c : Path.Combine(KSPUtil.ApplicationRootPath, "saves/" + HighLogic.SaveFolder + "/Ships/" + c);
        ConfigNode cn = File.Exists(craft) ? ConfigNode.Load(craft) : null;
        Assert("craft file " + craft, cn != null, "");
        if (cn == null) yield break;
        KerbalRoster roster = HighLogic.CurrentGame.CrewRoster;
        VesselCrewManifest man = roster.DefaultCrewForVessel(cn);
        Func<ProtoCrewMember, bool> fit = p => p != null && p.type == ProtoCrewMember.KerbalType.Crew
            && p.rosterStatus == ProtoCrewMember.RosterStatus.Available && !p.inactive;
        var used = new HashSet<string>();
        string want = step.GetValue("crew");
        foreach (PartCrewManifest pcm in man.GetCrewableParts())
        {
            ProtoCrewMember[] seats = pcm.GetPartCrew();
            for (int i = 0; i < seats.Length; i++)
            {
                ProtoCrewMember p = seats[i];
                bool first = used.Count == 0 && i == 0;
                if (first && want != null) { pcm.RemoveCrewFromSeat(i); p = roster[want]; Assert("crew " + want + " can fly", fit(p), p == null ? "not in roster" : p.rosterStatus + " inactive " + p.inactive); }
                else if (p != null && !fit(p))
                {
                    pcm.RemoveCrewFromSeat(i);
                    p = roster.Crew.FirstOrDefault(k => fit(k) && !used.Contains(k.name) && !man.Contains(k));
                }
                if (p != null && !used.Contains(p.name)) { if (pcm.GetPartCrew()[i] != p) pcm.AddCrewToSeat(p, i); used.Add(p.name); }
                else if (p == null && seats[i] != null) pcm.RemoveCrewFromSeat(i);
            }
        }
        vars["crew"] = string.Join(",", used.ToArray());
        Log("   crew aboard: " + (used.Count == 0 ? "(none)" : vars["crew"]));
        readyScene = GameScenes.LOADING;
        FlightDriver.StartWithNewLaunch(craft, EditorLogic.FlagURL ?? HighLogic.CurrentGame.flagURL, step.GetValue("site") ?? "LaunchPad", man);
        for (int t = 0; t < 240 && !(readyScene == GameScenes.FLIGHT && FlightGlobals.ready); t++) yield return new WaitForSecondsRealtime(1f);
        Assert("flight loaded", readyScene == GameScenes.FLIGHT && FlightGlobals.ready, "scene " + HighLogic.LoadedScene);
        yield return new WaitForSecondsRealtime((float)Num(cfg, "settle", 15));
    }

    // ---- focus: open the flight scene on a vessel of the save (by name) ----------------------------------
    IEnumerator Focus(ConfigNode step)
    {
        string name = step.GetValue("vessel");
        GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
        Game g = GamePersistence.LoadGame("persistent", HighLogic.SaveFolder, true, false);
        int idx = g == null ? -1 : g.flightState.protoVessels.FindIndex(pv => pv.vesselName == name || pv.vesselName.EndsWith("//" + name) || Localizer.Format(pv.vesselName) == name);
        Assert("focus: vessel " + name + " in the save", idx >= 0, g == null ? "save not loaded" : g.flightState.protoVessels.Count + " vessels");
        if (idx < 0) yield break;
        readyScene = GameScenes.LOADING;
        FlightDriver.StartAndFocusVessel(g, idx);
        for (int t = 0; t < 240 && !(readyScene == GameScenes.FLIGHT && FlightGlobals.ready); t++) yield return new WaitForSecondsRealtime(1f);
        Assert("focus: flight loaded on " + name, readyScene == GameScenes.FLIGHT && FlightGlobals.ready && FlightGlobals.ActiveVessel != null,
            "scene " + HighLogic.LoadedScene + ", active " + (FlightGlobals.ActiveVessel == null ? "-" : FlightGlobals.ActiveVessel.vesselName));
        yield return new WaitForSecondsRealtime((float)Num(cfg, "settle", 15));
    }

    static string TdWhere(Vessel v)
    {
        return v == null ? "no vessel" : v.vesselName + " " + v.situation + " alt " + Fmt(v.altitude) + " speed " + Fmt(v.srfSpeed)
            + " UT " + Fmt(Planetarium.GetUniversalTime());
    }

    // ---- fly: throttle up, next stage, log the flight ---------------------------------------------
    IEnumerator Fly(ConfigNode step)
    {
        if (PauseMenu.exists && PauseMenu.isOpen) PauseMenu.Close();
        FlightInputHandler.state.mainThrottle = (float)Num(step, "throttle", 1);
        if ((step.GetValue("stage") ?? "true").ToLowerInvariant() == "true") KSP.UI.Screens.StageManager.ActivateNextStage();
        double secs = Num(step, "seconds", 60);
        for (int t = 0; t < secs; t += 10)
        {
            yield return new WaitForSecondsRealtime(10f);
            Log("   fly t=" + (t + 10) + " " + TdWhere(FlightGlobals.ActiveVessel));
        }
        Vessel av = FlightGlobals.ActiveVessel;
        vars["situation"] = av == null ? "(none)" : av.situation.ToString();
    }

    // ---- land: wait for LANDED/SPLASHED coming from FLYING/SUB_ORBITAL --------------------------------
    IEnumerator Land(ConfigNode step)
    {
        Vessel v = FlightGlobals.ActiveVessel;
        if (v == null) { Assert("land: active vessel", false, "none"); yield break; }
        FlightInputHandler.state.mainThrottle = 0f;
        if ((step.GetValue("chutes") ?? "true").ToLowerInvariant() == "true")
        {
            var chutes = v.FindPartModulesImplementing<ModuleParachute>();
            foreach (ModuleParachute c in chutes) if (c.deploymentState == ModuleParachute.deploymentStates.STOWED) c.Deploy();
            Log("   land: " + chutes.Count + " parachutes armed (they open when the air is thick enough)");
        }
        int warp = (int)Num(step, "warp", 0);
        if (warp > 0) { TimeWarp.fetch.Mode = TimeWarp.Modes.LOW; TimeWarp.SetRate(warp, true); }
        bool airborne = false;
        double secs = Num(step, "seconds", 900);
        float t0 = Time.realtimeSinceStartup;
        int lastLog = -1;
        while (Time.realtimeSinceStartup - t0 < secs)
        {
            v = FlightGlobals.ActiveVessel;
            if (v == null) break;
            if (v.situation == Vessel.Situations.FLYING || v.situation == Vessel.Situations.SUB_ORBITAL) airborne = true;
            if (airborne && (v.situation == Vessel.Situations.LANDED || v.situation == Vessel.Situations.SPLASHED)) break;
            int el = (int)(Time.realtimeSinceStartup - t0);
            if (el / 10 != lastLog) { lastLog = el / 10; Log("   land t=" + el + " " + TdWhere(v)); }
            yield return new WaitForSecondsRealtime(1f);
        }
        if (warp > 0) TimeWarp.SetRate(0, true);
        v = FlightGlobals.ActiveVessel;
        vars["situation"] = v == null ? "(none)" : v.situation.ToString();
        Prove("land: LANDED or SPLASHED after FLYING or SUB_ORBITAL", airborne && v != null
            && (v.situation == Vessel.Situations.LANDED || v.situation == Vessel.Situations.SPLASHED), TdWhere(v) + ", airborne seen " + airborne);
    }

    // ---- dock: two vessels already near each other in orbit -------------------------------------------
    IEnumerator Dock(ConfigNode step)
    {
        Vessel a = FlightGlobals.ActiveVessel;
        if (a == null) { Assert("dock: active vessel", false, "none"); yield break; }
        string tname = step.GetValue("target");
        Vessel b = FlightGlobals.VesselsLoaded.Where(x => x != a && (tname == null ? x.FindPartModulesImplementing<ModuleDockingNode>().Count > 0 : x.vesselName == tname))
            .OrderBy(x => Vector3d.Distance(x.GetWorldPos3D(), a.GetWorldPos3D())).FirstOrDefault();
        ModuleDockingNode pa = a.FindPartModulesImplementing<ModuleDockingNode>().FirstOrDefault(PortFree);
        ModuleDockingNode pb = b == null ? null : b.FindPartModulesImplementing<ModuleDockingNode>().FirstOrDefault(PortFree);
        Assert("dock: target vessel loaded", b != null, tname ?? "nearest with a port");
        Assert("dock: free port on both", pa != null && pb != null, "active " + (pa != null) + ", target " + (pb != null));
        if (b == null || pa == null || pb == null) yield break;
        Log("   dock: " + a.vesselName + " -> " + b.vesselName + ", distance " + Fmt(Vector3d.Distance(a.GetWorldPos3D(), b.GetWorldPos3D())) + " m");
        int before = FlightGlobals.Vessels.Count;
        couples = 0;
        // turn the active vessel so its port faces the target's port, then put the ports gap metres apart
        Quaternion turn = Quaternion.FromToRotation(pa.nodeTransform.forward, -pb.nodeTransform.forward);
        a.SetRotation(turn * a.transform.rotation);
        yield return new WaitForFixedUpdate();
        Vector3d want = (Vector3d)pb.nodeTransform.position + (Vector3d)pb.nodeTransform.forward * Num(step, "gap", 0.3);
        a.SetPosition(a.GetWorldPos3D() + (want - (Vector3d)pa.nodeTransform.position));
        a.ChangeWorldVelocity(b.obt_velocity - a.obt_velocity);
        double secs = Num(step, "seconds", 30);
        for (int t = 0; t < secs && couples == 0; t++) yield return new WaitForSecondsRealtime(1f);
        if (couples == 0 && (step.GetValue("direct") ?? "false").ToLowerInvariant() == "true")
        {
            Log("   dock: the ports did not catch in " + secs + " s; calling DockToVessel");
            pa.DockToVessel(pb);
            for (int t = 0; t < 10 && couples == 0; t++) yield return new WaitForSecondsRealtime(1f);
        }
        yield return new WaitForSecondsRealtime(3f);
        Vessel now = FlightGlobals.ActiveVessel;
        bool one = now != null && now.parts.Contains(pa.part) && now.parts.Contains(pb.part);
        Prove("dock: onPartCouple/onDockingComplete and one vessel", couples > 0 && one,
            couples + " events, vessels " + before + " -> " + FlightGlobals.Vessels.Count + ", both ports in " + (now == null ? "-" : now.vesselName) + ": " + one);
    }

    // ---- recover --------------------------------------------------------------------------------------
    IEnumerator Recover(ConfigNode step)
    {
        string name = step.GetValue("vessel");
        Vessel v = name == null ? FlightGlobals.ActiveVessel : FlightGlobals.Vessels.FirstOrDefault(x => x.vesselName == name);
        if (v == null) { Assert("recover: vessel " + (name ?? "(active)"), false, "not found"); yield break; }
        string vn = v.vesselName;
        Assert("recover: " + vn + " can be recovered", v.IsRecoverable, "situation " + v.situation);
        lastRecovered = null;
        readyScene = GameScenes.LOADING;
        bool wasActive = v == FlightGlobals.ActiveVessel;
        if (wasActive) GameEvents.OnVesselRecoveryRequested.Fire(v);
        else ShipConstruction.RecoverVesselFromFlight(v.protoVessel, HighLogic.CurrentGame.flightState, true);
        double secs = Num(step, "seconds", 120);
        for (int t = 0; t < secs && lastRecovered != vn; t++) yield return new WaitForSecondsRealtime(1f);
        Prove("recover: onVesselRecovered for " + vn, lastRecovered == vn, "last recovered " + (lastRecovered ?? "(none)"));
        if (wasActive)   // recovering the active vessel leaves the flight: wait for the Space Center
        {
            for (int t = 0; t < 180 && readyScene != GameScenes.SPACECENTER; t++) yield return new WaitForSecondsRealtime(1f);
            Assert("recover: back at the Space Center", readyScene == GameScenes.SPACECENTER, "scene " + HighLogic.LoadedScene);
            yield return new WaitForSecondsRealtime((float)Num(cfg, "settle", 15));
        }
    }

    // ---- eva ----------------------------------------------------------------------------------------
    IEnumerator Eva(ConfigNode step)
    {
        Vessel v = FlightGlobals.ActiveVessel;
        string name = step.GetValue("name");
        ProtoCrewMember p = v == null ? null : v.GetVesselCrew().FirstOrDefault(c => name == null || c.name == name);
        if (p == null || p.KerbalRef == null) { Assert("eva: crew " + (name ?? "(first)") + " aboard", false, v == null ? "no vessel" : "not aboard or no Kerbal object"); yield break; }
        if (PauseMenu.exists && PauseMenu.isOpen) PauseMenu.Close();
        KerbalEVA eva = FlightEVA.SpawnEVA(p.KerbalRef);
        Assert("eva: SpawnEVA returned a Kerbal", eva != null, "hatch blocked or EVA not unlocked?");
        for (int t = 0; t < 15 && !(FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.isEVA); t++) yield return new WaitForSecondsRealtime(1f);
        Vessel now = FlightGlobals.ActiveVessel;
        vars["eva"] = now == null ? "(none)" : now.vesselName;
        Prove("eva: " + p.name + " is the active vessel, on EVA", now != null && now.isEVA && now.GetVesselCrew().Any(c => c.name == p.name), TdWhere(now));
    }
}
