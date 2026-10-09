# Unity: rebuild the KSPedia bundle of an old mod

Steps in the Unity editor of ksp-moddev (<http://localhost:6080/vnc.html>). Each step
says what to do and why. Mark **odd** whenever something looks different from what
the step describes, even if it works: that is the information the AI can't see.

## Open the project
In the desktop menu, open a terminal and run `unity-gui /work/MyMod/Unity`.
If Unity asks about "Non-Matching Editor Installation", click **Continue**.
**Why:** the project was saved by an older Unity; this upgrades it on this branch.
> Wait for the progress bar to finish before clicking anything else. The first import can take several minutes.

## Check the console
Open *Window → General → Console* and note the number of red errors.
**Why:** an error here usually means a script failed to compile, and the KSPAssets
menu will be missing in the next step.

## Check KSPAssets.dll
In the Project panel, select `Assets/Plugins/KSPAssets/KSPAssets.dll`. In the Inspector,
note the version and whether **Editor** is ticked under "Include Platforms".
**Why:** an old copy can survive the PartTools import and fail later with a `MissingMethodException`.

## Build the bundle
Go to *KSPAssets → Asset Compiler*, select `my_kspedia` in the list and click **Build**.
**Why:** `.ksp` bundles are loaded by KSP itself, and only this compiler makes them.
- Note how long it took and anything new in the console.

## Find the file
In the terminal, run `ls -l /work/MyMod/Unity/AssetBundles/`.
**Why:** the 1.12 compiler writes the file without the `.ksp` extension, and KSP only
loads `*.ksp`. Paste the listing in the note.
