# Test: RoverScience with the ROC fix

KSP-dev with RoverScience 2.3.5.10 and the test build of `RoverScienceCont.dll`.
The official build throws one exception on every landing for each flight scene
loaded before; with the fix it should throw none. Write down the clock time when
you start, so the log can be read from that point.

> Load the save **roverscience-test**, not your own career. Pressing F5 would overwrite the test's starting point.

## Start the game and load the save
Start KSP, write down the time and load **roverscience-test**.
**Why:** the time marks where the log reading starts.

## Launch the Bug-E Buggy from the runway
In the SPH, open the **Bug-E Buggy** (a stock craft: turn on the *Stock* craft
filter in the craft browser) and click **Launch**. It appears on the runway.
**Why:** a wheeled stock craft; when it starts moving it goes from "prelaunch" to
"landed", which fires the event RoverScience listens to.

## Drive along the runway
Turn on SAS if you like, hold **W** for about 10 seconds, then stop.
**Why:** the first change to "landed" in this scene.
- Note anything odd on screen, or a stutter.

## Revert and drive again, twice
**Esc → Revert to Launch**, drive for about 10 seconds. Do it once more (three flights in all).
**Why:** each revert reloads the flight scene. With the official build every
reload leaves a dead copy of `ROC_Class` listening, and exceptions start on the 2nd flight.
- Note whether a revert was slow or showed an error.

## Switch vessels through the Tracking Station
**Esc → Space Center**, open the **Tracking Station**, select the Bug-E Buggy and
click **Fly**. Drive for another 10 seconds.
**Why:** the other way to reload the flight scene, the one a career with many vessels uses.

## Open the RoverScience window
The Bug-E has no RoverScience part. Recover it, add the RoverScience brain in the
SPH, launch again and open the RoverScience window from the toolbar.
**Why:** the fix touches the same class; this checks nothing else broke. It's also
one more flight scene load.

## Quit to the main menu
**Esc → Quit to Main Menu**, write down the time in the note and close the game.
**Why:** the AI reads `KSP.log` between your two times and counts the exceptions.
On 2026-10-09 this test gave 5 flight scene loads and 0 exceptions.
