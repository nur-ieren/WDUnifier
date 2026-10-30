

\# WDUnifier



A Unity Editor tool for batch-switching Write Defaults settings across Animator Controllers.



\## Concept



WDUnifier was created to simplify the process of managing Write Defaults settings in avatar projects.



When working with multiple Animator Controllers, manually changing the Write Defaults setting of every state can be time-consuming.



This tool allows you to set the Write Defaults value of all states in multiple Animator Controllers to ON or OFF in one operation.



It uses Unity's AnimatorController API instead of directly modifying serialized YAML files.



\## Overview



\- \*\*Unity Version:\*\* 2022.3.22f1

\- \*\*Menu:\*\* `nur\_ieren > WDUnifier`

\- \*\*Display Languages:\*\* Japanese / English



\## Features



\- \*\*Multiple Target Selection Methods\*\* — Select Animator Controllers directly or specify a GameObject hierarchy.

\- \*\*Batch File Selection\*\* — Add multiple Animator Controllers by dragging and dropping them into the tool.

\- \*\*Automatic Controller Detection\*\* — Finds Animator Controllers used by Animator components under the specified GameObject.

\- \*\*NDMF Component Scanning\*\* — Detects Animator Controller references stored in other components, including components used by tools such as Modular Avatar's Merge Animator.

\- \*\*Animator Override Controller Support\*\* — Resolves Animator Override Controllers to their underlying Animator Controllers.

\- \*\*Inactive Object Support\*\* — Optionally includes inactive GameObjects when searching a hierarchy.

\- \*\*Write Defaults Switching\*\* — Sets all states in the collected Animator Controllers to either ON or OFF.

\- \*\*Preview\*\* — Displays the target Animator Controllers, total state count, and number of states that need to be changed.

\- \*\*Progress and Cancellation\*\* — Displays a progress bar during batch processing and allows the operation to be canceled.

\- \*\*Detailed Logs\*\* — Reports the number of changed states for each controller.

\- \*\*Undo Support\*\* — Records state changes in Unity's Undo system.

\- \*\*Bilingual Interface\*\* — Switches between Japanese and English, with the selected language saved in EditorPrefs.



\## Requirements



\- Unity 2022.3.22f1



\## How to use



1\. Open `nur\_ieren > WDUnifier`.

2\. Choose a target selection mode:

&#x20;  - \*\*File\*\* — Add Animator Controllers directly by dragging and dropping them or using the object field.

&#x20;  - \*\*GameObject\*\* — Assign a GameObject to search its hierarchy for Animator Controller references.

3\. If using GameObject mode, choose whether to include inactive objects.

4\. Select the desired Write Defaults value:

&#x20;  - `Set ON (1 / true)`

&#x20;  - `Set OFF (0 / false)`

5\. Click `Preview (check targets and change count)` to review the target controllers and the number of states that need changes.

6\. Click `Run Batch Switch` and confirm the operation.

7\. Check the log to review the results.



You can switch the interface language between Japanese and English using the toolbar at the top of the window.



\## Important notes



\- All states in the collected Animator Controllers are processed, including states inside nested state machines.

\- In GameObject mode, the tool scans Animator components and serialized object references in other components.

\- Animator Override Controllers are resolved to their underlying Animator Controllers.

\- Multiple references to the same Animator Controller are processed only once.

\- Only states whose current Write Defaults value differs from the selected value are modified.

\- Canceling an operation stops further processing, but controllers already processed remain changed.

\- Changes can be undone using Unity's Undo system, as long as the relevant Undo history remains available.

\- Back up your project before performing large batch operations.



\## Development



This project was created to make Write Defaults configuration more efficient and consistent across Animator Controllers.



\## Author



vor\_ges



\## Licence



This project is licensed under the MIT License. See LICENSE for details.



\[MIT License](https://opensource.org/license/mit)

