# Security review

## Scope

This review covers the C# source, project file, and embedded resources in the repository. It is a source review, not a guarantee about third-party dependencies, a future commit, or a downloaded binary.

## Findings and remediation

The reviewed build has no evidence of credential grabbing, browser-password or cookie access, clipboard logging, keylogging, webhook uploads, Discord/Telegram exfiltration, persistence, or hidden license telemetry. Hardware identifiers and remote license-validation code were removed from startup.

The original project was not safe to publish unchanged. It contained anti-debugger and virtual-machine checks, process blacklists, self-overwrite/self-delete routines, an `AnyDesk.exe` branch, and embedded batch files that cleared event logs, caches, prefetch data, and other forensic artifacts. Those checks, routines, remote unsigned batch download, and anti-forensic batch resources are removed from this build. The related destructive UI options are no longer offered.

## Remaining privileged behavior

- Administrator elevation is required because some registry and firewall operations are system-wide.
- Low-level mouse hooks observe mouse movement while the application is running.
- Selected registry presets can change Windows performance and input settings.
- The network optimizer runs a fixed list of visible `ipconfig` and `netsh` commands.
- Some UI buttons open external download URLs in the user's browser.

Review every `.reg` file before applying it. Never run a release binary you did not build or verify yourself. For a stronger release process, add reproducible builds, dependency pinning, signed releases, and automated static analysis.

## Reporting

Do not include credentials or private device identifiers in a public issue. Report suspected data collection or destructive behavior privately to the repository owner and include the commit, file, and observed command or URL.
