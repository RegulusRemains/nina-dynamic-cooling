# Release procedure

Dynamic Cooling releases are traceable from an immutable source tag to a single plugin DLL
and its SHA-256 digest. The GitHub workflow validates every source change. A tag beginning
with `v` also has to match the assembly version and produces a downloadable CI evidence
bundle; it does not publish or replace a GitHub Release automatically.

## Prepare

1. Update `Properties/AssemblyInfo.cs`, `CHANGELOG.md`, and user-facing documentation.
2. Restore the locked dependency graph and run the Windows build:

   ```powershell
   dotnet restore Tests/NINA.Plugin.DynamicCooling.Tests.csproj --locked-mode -p:Platform=x64
   dotnet test Tests/NINA.Plugin.DynamicCooling.Tests.csproj -c Release -p:Platform=x64 --no-restore
   dotnet build NINA.Plugin.DynamicCooling.csproj -c Release -p:Platform=x64 --no-restore
   ```

3. Merge the tested change before creating a release tag.

## Tag and publish

1. Create an annotated `v<assembly-version>` tag on the reviewed commit and push it.
2. Wait for the tag's **Build and test** run to pass.
3. Download the workflow evidence bundle and verify its checksum.
4. Create the GitHub Release from the same tag and attach only
   `NINA.Plugin.DynamicCooling.dll`.
5. Record the release asset's SHA-256 digest in the N.I.N.A. community manifest.
6. Validate the manifest repository and merge its reviewed pull request.

Never rebuild or replace an artifact on an existing tag. If a release is wrong, increment
the assembly version and publish a new tag so older rollback artifacts remain auditable.

## Existing v1.8.1.0 provenance

The historical `v1.8.1.0` release predates this workflow and remains immutable:

- source tag: `v1.8.1.0`
- source commit: `35d3889a294711411b75218f77438bffed11acbb`
- artifact: `NINA.Plugin.DynamicCooling.dll`
- SHA-256: `e23fd61b81f863d2e3349d40470d20e3c54f5656a7549d6cd02db48884b60e40`

The community manifest must reference that existing release asset and digest. It must not
substitute a DLL rebuilt from the tag.
