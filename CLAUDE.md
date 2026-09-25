# Pug.Sqetch

DevOps database schema upgrader. A **project** contains **plans** (units of schema change),
each plan contains **steps** with three SQL scripts (`deploy.sql`, `verify.sql`,
`rollback.sql`). Plans are grouped into **releases** that form a single linear lineage: every
release except the first names exactly one predecessor (`Dependency`), and a release may have
at most one dependant. Finalizing a release freezes it and its plans. The `bundle` command
packages deployable plans plus a manifest for deployment pipelines; the separate
**sqetch-deploy** CLI consumes those bundles and deploys them to a database through
pluggable database drivers and change journal writers.

## Commands

```sh
dotnet build                 # full solution
dotnet test                  # all test projects; must be green before any commit
dotnet run --project Pug.Sqetch.Cli -- <args>   # run the 'sqetch' CLI
dotnet run --project Pug.Sqetch.Deployment.Cli -- <args>   # run the 'sqetch-deploy' CLI
```

## Solution layout

| Project | Role |
|---|---|
| `Pug.Sqetch.Models` (net8.0) | Plain records: definitions, criteria, `StepScripts`, `UserInfo` |
| `Pug.Sqetch.Abstractions` (net8.0) | `IProject`/`IReadOnlyProject`, `IScriptsStore`, domain exceptions |
| `Pug.Sqetch` | The engine (`Project`): business rules, dependency ordering, semaphores |
| `Pug.Sqetch.ProjectInfoStore.Abstractions` | `IProjectInfoStore` contract |
| `Pug.Sqetch.ProjectInfoStores.FileSystem` | Git-friendly file store: `ProjectPaths`, sharding, plan index, `NameRules` |
| `Pug.Sqetch.Bundling.Abstractions` | Bundling contracts: `Bundle` model, `IBundleType`/`IBundleWriter`/`IBundleLayout`, registry interfaces, exceptions |
| `Pug.Sqetch.Bundling.BundleTypes` | zip / tar / tar.gz / directory implementations + `RegisterBundleTypes()` |
| `Pug.Sqetch.Bundling.Layouts` | `DefaultBundleLayout` + `RegisterLayouts()` |
| `Pug.Sqetch.Bundling` | `BundleBuilder` orchestration + registry implementations |
| `Pug.Sqetch.Cli` | Spectre.Console.Cli command tree (`SqetchApp.Configure`), the only layer touching `ProjectPaths` for output |
| `Pug.Sqetch.Deployment.Abstractions` (net8.0) | Deployment contracts (ns `…Deployment.DatabaseDriver.Abstractions`): `IDatabaseDriverFactory`/`IDatabaseDriver`/`IDatabaseTransaction`, `IChangeJournalWriter`, registry interfaces, deployment exceptions |
| `Pug.Sqetch.Deployment` (net8.0) | `DeploymentEngine` (deploy flow), `BundleValidator`, driver/journal registries, `IDeploymentListener` |
| `Pug.Sqetch.Deployment.Cli` | The 'sqetch-deploy' command tree (`SqetchDeployApp`), `TypeRegistrar` DI seam, dynamic `--<driver>-<param>` switch parsing |
| `Pug.Sqetch.Tests*` | xunit test projects, one per tier |

## Architecture rules

- **Layering**: the store filters, the engine orders, the CLI presents. Ordered read APIs
  (`GetReleases`, `GetPlans`, `GetSteps`) return dependency-chronological order — never
  re-sort downstream. Only the CLI may use `ProjectPaths` (for printing paths).
- **Storage is git-legibility-first**: state changes move folders (plan → release folder on
  assignment; release → its shard on finalization). Only *finalized* releases are sharded;
  unfinalized ones live at `releases/<name>/`. `ReleaseDirectory` probes both locations —
  never scan `releases/` (the plan index and `EnumerateReleaseDirectories` exist for that).
- **`plan-index` is derived data**: plan.json files and folder locations are the source of
  truth; the index is rebuildable (`sqetch project reindex`).
- **Pluggable families** (sharding strategies, bundle types, bundle layouts, database
  drivers, change journal writers): contracts in an `.Abstractions` project, implementations
  in sibling projects, a registry keyed by name (`OrdinalIgnoreCase`). Bundling and
  deployment registries ship **empty**; the host registers built-ins via the `Register*()`
  extension methods (see `BundleCommand`, `SqetchDeployApp.CreateDefaultRegistrar`). No real
  database driver or journal writer exists yet — tests use fakes.
- **Bundles are read through the same families**: `IBundleType.Open` returns an
  `IBundleReader` (archives extract to a temp dir deleted on dispose); `IBundleLayout`
  translates its on-disk manifest schema to/from the layout-neutral `BundleManifest`
  (`ReadManifest`) and owns entry pathing (`ScriptPath`). Manifest array order IS the
  deployment order.
- **Bundles carry the release lineage**: every manifest release records its `dependency` (the
  release it follows; empty starts the lineage), and a bundle's releases must form a
  contiguous chain — a manifest omitting `dependency` normalizes it to the preceding release,
  since array order is already the contract. `bundle --since <release>` emits a *continuation
  bundle* holding only the releases after the named one.
- **Deploy semantics** (`DeploymentEngine`): the journal alone decides where deployment starts,
  in four branches — nothing journaled deploys the bundle whole; an **incompletely** deployed
  release must be *contained* in the bundle and resumes at the plan after its last deployed one;
  a **completely** deployed release hands over to whichever group *depends* on it, which may be
  a release the bundle does not contain (continuation bundle). Each branch calls the private
  `Deploy( groups, lastGroup, lastPlan )`: `lastPlan` resumes inside the release holding it
  (and journals that release's completion when it is its last plan), `lastGroup` alone starts at
  the group depending on it, neither starts at the first group — refusing when the first group
  has a dependency. `IChangeJournalWriter.GetLatestRelease` returns `JournaledRelease?`
  (`null` = fresh database, `Name == ""` = the unreleased-plans pseudo-release, `Complete` = its
  `ReleaseDeployed` was journaled) and `GetDeployedPlans` is **ordered**, oldest first — the
  engine takes its last element. The pseudo-release depends on the bundle's last real release,
  so it is an ordinary link in the chain; nothing may depend on *it*, so a database left at the
  pseudo-release is refused rather than matched to a lineage-starting bundle. A completed
  release is never resumed into, so plans a bundle gained for one are not deployed — test
  databases are single-use. Re-deploying an unchanged bundle is `NothingToDeploy`, not an error.
  Mismatches are `IncompatibleBundleException` (the bundle is well formed but does not fit the
  database's state); `InvalidBundleException` stays for malformed manifests. Commit at plan or
  release boundaries (`ReleaseDeployed` is always journaled in the same transaction as the
  release's last plan); on failure roll back the open transaction — earlier commits stand. The
  rollback command parses fully but is not implemented yet.
- **Names** (`NameRules` in the FileSystem store, surfaced early via CLI `NameValidation`):
  letters, digits and `-_+()@#.`; must start with a letter or digit, must not end with `.`,
  max 128 chars. Names become path segments and git paths.
- **`GetStepScripts` ownership**: the engine returns open streams the caller must dispose,
  and throws `MissingStepScriptsException` when a script file is missing;
  `VerifyStepScripts( plan )` checks a whole plan.

## Conventions

- Tabs for indentation; spaces inside parentheses: `Foo( bar, baz )`; file-scoped namespaces.
- Exceptions: plain classes with get-only properties and an optional message via `base(...)`
  (see `UnknownPlanException`); mapped to friendly CLI messages in `CliErrors.Describe`,
  surfaced by the central `SetExceptionHandler` in `SqetchApp` with exit code 1.
- Tests: xunit. sqetch CLI tests drive the real command tree through `CommandAppTester` and
  switch the process-wide current directory — every test class that does so must join
  `[Collection( "cli" )]` to stay sequential. sqetch-deploy CLI tests instead pass absolute
  paths and inject fakes via the production `TypeRegistrar` (`CommandAppTester( registrar )`),
  so they need no collection. Store/engine tests build on `TempProject`,
  `TestData` and `ShardingCases` from `Pug.Sqetch.Tests.Stores.FileSystem/TestSupport.cs`;
  git legibility is asserted with real `git` via the `Git` helper.
- Verify changes with the full `dotnet test` plus, for user-visible behavior, a live CLI run
  in a temp directory (`project user`, `project init`, then the flow under test).
