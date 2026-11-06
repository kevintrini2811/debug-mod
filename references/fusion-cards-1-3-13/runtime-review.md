# Debug1.3.13 new fusion cards: PASS

Verified installed Debug DLL SHA2566443f8c604ae3b83a48e787fc5b0246a23d38d316919ec1ec9e217a01746eef5 and runtime startup version1.3.13.

Independently compared each original atlas resource against GitHEAD by its own rectangle: all331 previous card images are byte/pixel-identical. Exactly three resources were added:1059 Squashwood,1061 Torchweed and1155 RoastedSpikeweed, giving334 atlas entries.

In a fresh temporary Pool Debug session, selected all three actual registered cards using the native chooser and completed both plant/zombie selection phases. The three cards are visible in the chooser bank and gameplay bank. debug.ui confirms each uses its exact expected128×176 rectangle in the embedded2048×3696 atlas; no fallback portrait was used. Screenshots were visually reviewed and show the distinct source models and their expected prices.

Evidence:runtime.json,chooser-three-new-cards.png,bank-three-new-cards.png. A transient BOARD_NOT_READY response during initial scene creation was handled as fixture readiness, not a product failure. No new runtime errors or exceptions. Test board left empty, paused,1×; no profile/save/default setting changed. No partial release commit was made.
