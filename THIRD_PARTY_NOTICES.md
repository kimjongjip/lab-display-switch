# Third-party components

`vendor/vdd/` contains the Windows x64 Virtual Display Driver package and its MIT
license. Keep `vendor/vdd/LICENSE.txt` with redistributed copies.

- Distribution: https://github.com/VirtualDrivers/Virtual-Driver-Control
- Pinned commit: `a1ae80aa1407402468dfb9893530f933cb6da1cf`
- Driver source: https://github.com/VirtualDrivers/Virtual-Display-Driver
- INF version: `08/14/2025,22.50.22.79`
- SHA-256: `vendor/vdd/provenance.json`; the installer pins the same hashes.
- DLL/catalog signatures were checked on the development PC. Recheck on the
  target PC because trust and revocation depend on its environment.

The app's license does not replace the driver license. Replacing driver binaries
requires origin, license, signature, hash and compatibility checks.

Implementation references (not bundled libraries):

- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig
- https://github.com/lihas/windows-DPI-scaling-sample
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mousekeys
- https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/iddcx/nf-iddcxmonitorsetuphardwarecursor

Chrome Remote Desktop and other remote transports are not bundled.
