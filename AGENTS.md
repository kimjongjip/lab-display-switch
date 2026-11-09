# Working on Lab Display Switch

Read `README.md`, `docs/PORTING.md`, `docs/ARCHITECTURE.md`, and
`docs/RECOVERY.md` before changing display behavior. This is a Windows x64
companion for an existing remote desktop product, not a remote-access server.

## First actions on a new PC

1. Inspect OS, physical GPU names, physical monitor count/orientation/DPI, existing
   virtual drivers, and whether the user is physically present or remote.
2. Run `scripts/build.ps1 -Test`. This does not install drivers or switch displays.
3. Use `scripts/configure-machine.ps1 -ListGpus`. Choose a GPU with the user using
   the hardware evidence; never copy the original lab GPU name or monitor IDs.
4. Follow the staged install and validation procedure in `docs/PORTING.md`.

## Constraints and tests

- C# 5, .NET Framework, Windows Forms, x64; no SDK or NuGet needed. Compile with
  the bundled Windows Framework compiler, warnings treated as errors.
- Exactly two physical and two selected virtual displays are currently assumed.
  One/three monitors, ARM64, headless first install, and mixed GPUs need explicit
  design changes and validation. Do not claim they already work.
- Keep user sessions and research processes alive. Never reboot, log off, kill
  unrelated apps, disable the real GPU, or change unrelated virtual drivers.
- Display changes must preserve the independent guard, transaction journal,
  validation, and rollback. Never mark `Tested=true` to skip human verification.
- Remote mode must not restore physical output merely because the client
  disconnects or the tray app exits. Changes to this rule affect privacy.
- Per-monitor DPI setters are undocumented requests (-3/-4). Keep range checks,
  actual-scale checks, virtual-only writes, and post-write verification.
- `-Test` runs logic tests. `-WindowIntegration`, `--trial`, hotkey trials,
  driver installation/restart and actual remote mode are live desktop mutations.
  Explain the visible effect and perform them only within the user's requested
  scope, with a usable rollback path. Do not run them in ordinary CI.
- `scripts/test-tray-hotkeys.ps1` requires the exact installed app path. It is
  optional and requires a previously verified remote setup; run from lab mode.
- Do not publish `artifacts`, `dist`, AppData state, logs, screenshots, monitor
  IDs, window handles/PIDs, credentials, or original-machine repair scripts.
- Never install a newly built version over a running executable. Update both UI
  and guard in lab mode; old guards can drop newly added settings on write.
- Keep package hashes and third-party notices intact. Replacing a driver package
  requires independent origin, hash, signature, license, and compatibility review.

## Useful entry points

`src/Displays.cs`: topology and monitor mapping; `Dpi.cs`: scaling;
`Controller.cs`: transactions; `Windows.cs`: window restoration;
`Guard.cs`: rollback/privacy/awake state; `Installer.cs`: driver installation;
`CursorRecovery.cs`: narrowly gated missing-mouse recovery;
`Models.cs`: persisted state. Update docs and regression tests with behavior changes.
