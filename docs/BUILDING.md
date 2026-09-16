# Building and developing

1. Install Visual Studio 2022 with the .NET desktop workload and the .NET Framework 4.8.1 targeting pack.
2. Restore the packages referenced by `packages.config`.
3. Build Debug while changing code:

   ```powershell
   dotnet msbuild "LegitX V2.csproj" /t:Build /p:Configuration=Debug /v:minimal
   ```

4. Build Release for a distributable artifact and inspect the output directory before packaging it.

The project is a classic .NET Framework project, so `dotnet build` may not provide the same experience as Visual Studio MSBuild on every machine. If the Guna UI dependency cannot be loaded, use the full Release output produced by MSBuild and confirm the Costura/Fody output is present.

Changes that touch hooks, registry presets, process launching, URLs, or elevation need a manual review on a disposable Windows installation. Keep generated `*.Designer.cs` files synchronized with the WinForms designer and document any new permission or data flow in `docs/SECURITY.md`.
