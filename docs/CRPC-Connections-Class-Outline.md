# Class Outline: Connecting CH5 Media Players to NAX with CRPC

**Length:** 30 minutes (28 min content + 2 min buffer)
**Audience:** C# programmers with minimal experience (Crestron background expected)
**Companion handout:** `CRPC-Connections-HowTo.md`
**Goal:** Attendees leave able to (1) add a panel, (2) add a hardware player, and (3) use the log to
tell which side is misconfigured.

---

## Before class

- [ ] Processor loaded with the current build, NAX online, two panels (0x03, 0x04) open side by side.
- [ ] Log page (CwsDebug) open on the projector, filtered to Notice and above.
- [ ] Visual Studio open with three tabs: `MediaPlayerId.cs`, `HardwareSetup.cs` (CRPC section), `UI.cs` (Media Player section).
- [ ] The big-picture diagram (How-To §2) drawn on the whiteboard or on one slide.
- [ ] Handout printed or shared.
- [ ] Backup: screenshots of the log for each demo step, in case the hardware misbehaves.

---

## Timeline at a glance

| Time | Segment | Format |
|---|---|---|
| 0:00–0:03 | 1. The problem | Talk |
| 0:03–0:08 | 2. Communication flow: two classes and a broker | Whiteboard |
| 0:08–0:11 | 3. `MediaPlayerId`: the address | Code |
| 0:11–0:15 | 4. Hardware side | Code |
| 0:15–0:22 | 5. UI side | Code + live demo |
| 0:22–0:26 | 6. Three C# ideas that make it work | Talk + code |
| 0:26–0:28 | 7. Troubleshooting with the log | Live demo |
| 0:28–0:30 | 8. Recap and questions | Talk |

---

## 0. Introduction

**Bullets**
- [segue] Now that you've seen how to select NAX audio sources and tie button event handlers into the generated contracts, we need to enable control of the NAX streaming media players.
- We are going to walk through two new classes you can use with one or more touchpanels and one or more streaming media players.

---

## 1. The problem (0:00–0:03)

**Key points**
- The CH5 Media Player on the panel and the players in the NAX don't talk to each other directly. Our program passes messages between them.
- The messages are **CRPC** strings, which are simply JSON formatted data that go back and forth. We don't need to read or understand them. We just deliver them to the right place.
- Three jobs: tell the panel's media player **the source changed**, commands **in**, feedback **out**.

**Say:** "Your job isn't to understand CRPC. Your job is to be a good telephone switchboard operator."

**Transition:** "Rather than mix this switchboard code into the `NaxAmp`, `HardwareSetup`, and `UI` classes, we use two new classes to tie it all together."

**Check for understanding:** "What previously covered class comes to mind when I mention `telephone switchboard`?"  (*The `Broker` class.*)

---

## 2. Communication flow (0:03–0:08)

**Draw the diagram** (How-To §2). Build it from left to right while talking:
1. Panel on the left, NAX on the right.
2. `CrpcUiConnection` next to the panel ("one per panel").
3. `CrpcHardwareConnection` next to the streaming media player hardware ("one per DM-NAX-8ZSA").
4. Message Broker in the middle with two arrows: **command key →** and **← feedback key**.

**Key points**
- The two classes **never know about each other.** They only meet at the broker.
- **Hardware side:** subscribes at startup and never lets go. It's always listening.
- **UI side:** subscribes only while the user is looking at a player. It switches when the user switches players. It disconnects when the user closes the media player.
- Two panels on the same player both get the feedback. That's intended and matches SIMPL.

**Check for understanding:** "If I add a panel, which side of the diagram gets a new box?" (*The UI side. One new `CrpcUiConnection`.*)

---

## 3. `MediaPlayerId`: the address (0:08–0:11)

**Show:** `Configuration/MediaPlayerId.cs`

**Key points**
- `new MediaPlayerId("Nax1", 1)` means "Player 1 on Nax1."
- It **builds the broker keys** for you. Show `CommandKey` and `FeedbackKey`. Never type those strings by hand.
- It's a `record`, so two IDs with the same values are equal. (We'll use this in a minute.)
- A blank hardware ID crashes at startup **on purpose**. Fail loudly early rather than quietly later.

**Show:** `ControlSystem.SetupNaxSources()`, where each media-player source gets its `MediaPlayerId`.

---

## 4. Hardware side: `CrpcHardwareConnection` (0:11–0:15)

**Show:** `HardwareSetup.cs`, the Media Player CRPC block.

**Walk through the two steps:**
1. **Create it:** hardware ID, player count, and a lambda that says *how* to send to the NAX.
2. **Feed it:** the NAX's `CrpcMessageReceived` event calls `SendFeedback`.

**Key points**
- The class doesn't know what a NAX is. The lambda teaches it. → Another brand of player can use the same class.
- One per device. A duplicate ID (even `nax1` vs `Nax1`) throws at startup.
- The hardware ID **must match** the ID in your `MediaPlayerId`s. This is the #1 setup mistake.

**Show briefly:** How-To §4.4, "Adding a second NAX." It's about six lines, and `UI.cs` doesn't change.

---

## 5. UI side: `CrpcUiConnection` (0:15–0:22)

**Show:** `UI.cs`, the constructor where `CrpcUiConnection` is created with two lambdas (Message_FB and CRPC_FB).

**Put the three methods on the board:**

| Method | When |
|---|---|
| `Connect(player)` | User picks a media-player source |
| `SendCommand(crpc)` | Panel's Media Player sends CRPC |
| `DisconnectAsync()` | User leaves, picks a non-player source, or the panel goes offline |

**Walk through the five call sites** (How-To §5.3). Keep each to about 30 seconds:
1. CRPC event → `SendCommand`
2. Source select (player) → show page, then `Connect`
3. Source select (not a player) → `_ = DisconnectAsync()`
4. Return button → `await DisconnectAsync()`, **then** flip the page
5. Panel offline → `_ = DisconnectAsync()`

**Live demo (about 3 min)**
1. Panel A: select Media Player 1. In the log, point out the source-change message (`tag` 01) and "connected to …CrpcFb".
2. Panel B: select Media Player 1. Point out `tag` 02. Press Play on B, and **both panels update.**
3. Panel A: press Return. In the log, point out the source-change message with `src 0` and "unsubscribing."

**Key point, the one rule to remember:** *Don't flip away from the Media Player page directly. Await `DisconnectAsync` first.* The panel needs about 200 ms with the page still open to process the "source 0" message. Otherwise the next connection can fail.

**Check for understanding:** "What should a new panel class need to change in `CrpcUiConnection`?" (*Nothing. Create one and wire up the five call sites.*)

---

## 6. Three C# ideas that make it work (0:22–0:26)

Pick these three. The rest are in the handout (How-To §9).

**a. Callbacks (named delegates), about 1.5 min**
- "Here's a method; call it when you need to send." That's all a delegate is. We give each callback a name (`SendCrpcToHardware`, `SendMessageFbToUi`, `SendCrpcFbToUi`) so IntelliSense shows what the arguments mean.
- That's why neither class mentions `NaxAmp` or the Construct contract.

**b. Lambda vs. named method, about 1.5 min**
- Hardware side uses a lambda because it never unsubscribes.
- UI side uses the named method `ForwardCrpcFbToUi` because to **remove** a subscription, you must hand the broker the **same** method you added. You can't hand it the same lambda twice.
- *Optional, if the room is with you:* show the `for`-loop capture comment in `CrpcHardwareConnection`. It's a classic bug and worth knowing exists.

**c. `async` / `await`, about 1 min**
- `await Task.Delay(200)` waits without freezing the panel. `Thread.Sleep` freezes it.
- `async void` is **only** for event handlers. Everywhere else, use `async Task`.
- `_ = DisconnectAsync();` means "start it, don't wait." Use it when nothing has to happen afterward.

---

## 7. Troubleshooting with the log (0:26–0:28)

**Live demo (pre-staged, or show screenshots):** change one source's `MediaPlayerId` to `"Nax2"` (no such hardware), select it, and press Play.

**Point out:** *"UI instance 1: no hardware is listening for Nax2.Player1. Check that its HardwareId and PlayerNumber match…"*

**Key point:** The log tells you **which side** is wrong. Show How-To §8 (Troubleshooting table) and tell them it's the first place to look.

---

## 8. Recap and questions (0:28–0:30)

**Three takeaways (say them, then leave them on screen):**
1. **One `CrpcHardwareConnection` per device, one `CrpcUiConnection` per panel.** They meet only at the broker.
2. **`MediaPlayerId` is the address.** The hardware ID must match on both sides, and you never type broker keys by hand.
3. **`await DisconnectAsync()` before leaving the Media Player page.**

**Likely questions, with short answers**

| Question | Answer |
|---|---|
| "Why do both panels get the feedback?" | They're watching the same player. That's intended and matches SIMPL. |
| "What's `src`, and where does it come from?" | The Media Player's source number. `NaxSourceConfiguration` hands them out (1, 2, 3…) in the order players are added. 0 means none. Don't hard-code them. |
| "What's the `tag`?" | Each panel's `CrpcUiConnection` instance number, assigned in the order panels are created. You don't set it. |
| "Can I use this with a non-Crestron player?" | Yes. Pass a callback that sends to that device, and call `SendFeedback` from its feedback event. If it numbers players from 0, translate in the callback. |
| "Why 200 ms?" | It was found by testing. It's long enough for the panel to process the source change. We don't break the "100ms Rule" for instantaneous feedback because the button feedback changes immediately. |
| "Why not just make one big class?" | Don't make classes bigger than necessary. The hardware lives as long as the program; panel connections come and go. Different lifetimes, different classes. |

---

## Optional take-home exercise (if you want to extend past 30 min)

1. Add a third panel at IP ID `0x05`. Confirm in the log that it gets `tag` 03.
2. Add "Media Player 3" (`new MediaPlayerId("Nax1", 3)`) to zones 1 and 2. Confirm that it gets its own `src` number and that both panels can control it.
3. Break it on purpose: change `"Nax1"` to `"nax1"` in one `MediaPlayerId`. Predict the log message before you run it. (*Hint: the broker keys are case-sensitive, even though the duplicate-ID check isn't.*)
4. Add logic to the `CrpcUiConnection` class to trigger DisconnectAsync() whenever a program stopping event is raised.
