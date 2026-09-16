# Privacy and data flow

LegitX V2 is a local desktop utility. It does not have an analytics SDK, account system, cloud database, browser-profile reader, credential store reader, or automatic upload pipeline.

While running, Windows delivers mouse movement to the low-level hook so the app can process sensitivity and prediction. Settings are stored locally through the existing registry manager. The app may inspect the foreground window and running browser process names to decide whether to open a user-requested download link; it does not read browser history, cookies, passwords, or page contents.

Network requests are limited to user-triggered URL availability checks and opening external links. No device identifier is sent to a server by the reviewed build. If you add telemetry or online licensing later, update this document and the security review before release.
