# Test: RoverScience with the ROC fix

KSP-dev with the test build of `RoverScienceCont.dll`. Write down the clock time
when you start, so the log can be read from that point.

> Load the save **rover-fix-test**, not your own career. Pressing F5 would overwrite the test's starting point.

## Start the game and load the save
Start KSP, load **rover-fix-test** and go to the rover on the launchpad.
**Why:** the bug only shows up after a few scene changes, so we count them from here.

## Change scenes three times
Go to the Space Center and back to the rover, three times.
**Why:** every flight scene used to leave one dead ROC_Class behind.

## Land the rover
Drive off the launchpad onto the grass. Jump with a small hop if it doesn't
switch to "Landed" by itself.
**Why:** each landing fires the event that the dead copies used to answer with an exception.
- Note whether the game stuttered when it landed.

## Use the rover science console
Open RoverScience's console and do one analysis.
**Why:** the fix touches the same class; this checks nothing else broke.

## Quit to the main menu
Quit and write the clock time in the note.
**Why:** the AI reads `KSP.log` between your two times and counts the exceptions.
