// Skeleton addon: logs one line at the main menu. Replace with the mod.
using System.Reflection;
using KSP.Localization;
using UnityEngine;

[assembly: AssemblyVersion("0.1.0.0")]   // tools/validate.py checks this against the .version file

namespace {{NAME}}
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class Main : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("[{{NAME}}] " + Localizer.Format("#LOC_{{NAME}}_hello", "Kerbal"));
        }
    }
}
