# Pug.Sqetch

DevOps database schema upgrader. A **project** contains **plans** (units of schema change),
each plan contains **steps** with three SQL scripts (`deploy.sql`, `verify.sql`,
`rollback.sql`). Plans are grouped into **releases** that form a single linear lineage: every
release except the first names exactly one predecessor (`Dependency`), and a release may have
at most one dependant. Finalizing a release freezes it and its plans. The `bundle` command
packages deployable plans plus a manifest for deployment pipelines.

## Commands

```sh
dotnet build                 # full solution
dotnet test                  # all test projects; must be green before any commit
dotnet run --project Pug.Sqetch.Cli -- <args>   # run the 'sqetch' CLI
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
- **Pluggable families** (sharding strategies, bundle types, bundle layouts): contracts in an
  `.Abstractions` project, implementations in sibling projects, a registry keyed by name
  (`OrdinalIgnoreCase`). Bundling registries ship **empty**; the host registers built-ins via
  the `Register*()` extension methods (see `BundleCommand`).
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
- Tests: xunit. CLI tests drive the real command tree through `CommandAppTester` and switch
  the process-wide current directory — every test class that does so must join
  `[Collection( "cli" )]` to stay sequential. Store/engine tests build on `TempProject`,
  `TestData` and `ShardingCases` from `Pug.Sqetch.Tests.Stores.FileSystem/TestSupport.cs`;
  git legibility is asserted with real `git` via the `Git` helper.
- Verify changes with the full `dotnet test` plus, for user-visible behavior, a live CLI run
  in a temp directory (`project user`, `project init`, then the flow under test).
