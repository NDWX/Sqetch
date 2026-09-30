# Pug.Sqetch

DevOps database schema upgrader. A **project** contains **plans** (units of schema change),
each plan contains **steps** with three SQL scripts (`deploy.sql`, `verify.sql`,
`rollback.sql`). Plans are grouped into **releases** that form a single linear lineage: every
release except the first names exactly one predecessor (`Dependency`), and a release may have
at most one dependant. Finalizing a release freezes it and its plans. The `bundle` command
packages deployable plans plus a manifest for deployment pipelines; the separate
**sqetch-deploy** CLI consumes those bundles and deploys them to a database through
pluggable database drivers, journaling through SQL the project maintainer authors.

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
| `Pug.Sqetch.Deployment.Abstractions` (net8.0) | Deployment contracts (ns `…Deployment.DatabaseDriver.Abstractions`): `IDatabaseDriverFactory`/`IDatabaseDriver`/`IDatabaseTransaction`, `JournalingParameter`, `JournaledRelease`, registry interface, deployment exceptions |
| `Pug.Sqetch.Deployment` (net8.0) | `DeploymentEngine` (deploy flow), `StatementJournal`, `BundleValidator`/`ValidatedBundle`, driver registry, `IDeploymentListener` |
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
- **Journaling statements are project-level and not frozen**: they live in `journaling/` at the
  project root, written atomically like every other store file, and — unlike step scripts — have no
  finalized-release guard, so they can change after a release is finalized while an already-built
  bundle keeps the SQL it was built with. `ProjectPaths` owns the paths and
  `JournalingSlots.FileName` the names, so the store and the bundle layout cannot drift apart.
- **`plan-index` is derived data**: plan.json files and folder locations are the source of
  truth; the index is rebuildable (`sqetch project reindex`).
- **Pluggable families** (sharding strategies, bundle types, bundle layouts, database
  drivers): contracts in an `.Abstractions` project, implementations
  in sibling projects, a registry keyed by name (`OrdinalIgnoreCase`). Bundling and
  deployment registries ship **empty**; the host registers built-ins via the `Register*()`
  extension methods (see `BundleCommand`, `SqetchDeployApp.CreateDefaultRegistrar`). No real
  database driver exists yet — tests use fakes. Journaling is deliberately *not* a family:
  the SQL is the project's, so there is nothing to select at deploy time and no
  `--driver`/`--journal` pair to mismatch.
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
- **Journaling is project-authored SQL** (`JournalingSlot`, `JournalingStatements` in
  `Pug.Sqetch.Models`): fifteen slots — twelve deployment events, the two queries
  `GetLatestRelease`/`GetDeployedPlans`, and `PrepareJournal` — set per project via
  `IProject.SetJournalingStatement`, stored as `journaling/<slot>.sql`, and carried in the bundle.
  A slot may hold several statements, separated by a line whose trimmed content is exactly `;;`;
  that is the **only** parsing Sqetch does to the SQL, so a `$$ … $$` body containing a bare `;;`
  alone on a line would split wrongly. Query slots hold exactly one statement. Parameters are
  supplied by name without a provider prefix (`@project` on every slot, plus `@release`, `@plan`,
  `@step`, `@description` as the slot carries them — see `JournalingSlots.ParameterNames`); binding
  is the driver's job and Sqetch never scans SQL for placeholders. Deliberately absent: a timestamp
  (`now()` evaluates server-side, so a skewed deployment host cannot disorder the journal) and an
  actor (`current_user`). `bundle` refuses until all fifteen are set, naming every missing one;
  rollback slots are required even though rollback deployment is not implemented.
  CLI: `sqetch journaling list [--statements] [-o <FORMAT>]`, `parameters [<slot>] [-o <FORMAT>]`
  (answers from the contract alone, so it needs no project), `print <slot>` (verbatim, no markup,
  so `> file.sql` round-trips) and `set <slot>` taking exactly one of an SQL argument, `--file` or
  `--stdin`. SQL beginning with `--` cannot be passed as the argument — a leading-dash positional
  parses as an option — so a comment-first statement needs `--file` or `--stdin`.
- **Query result contracts** (`StatementJournal`): read by **ordinal**, first N columns are the
  contract and extras are ignored — Oracle folds unquoted identifiers, and `GetOrdinal`'s case
  sensitivity is provider-defined. `GetLatestRelease` returns zero or one row (column 0 release
  name, column 1 completed); more than one row throws, and a NULL release name throws rather than
  being coerced, because `""` already means the pseudo-release. `completed` coerces from bool,
  any numeric (`!= 0`), or `1/y/yes/t/true` case-insensitively. Failures are
  `JournalingStatementException` naming the slot and the 1-based statement within it.
- **Deploy semantics** (`DeploymentEngine`): the journal alone decides where deployment starts,
  in four branches — nothing journaled deploys the bundle whole; an **incompletely** deployed
  release must be *contained* in the bundle and resumes inside it; a **completely** deployed
  release hands over to whichever group *depends* on it, which may be a release the bundle does
  not contain (continuation bundle). `deployedPlans` resumes inside the journaled group,
  skipping the plans it **names** and deploying the rest in bundle order (and journaling that
  release's completion when none are left, without journaling its start again); the journaled
  group alone starts at the group depending on it; neither starts at the first group — refusing
  when the first group has a dependency. `GetLatestRelease` returns `JournaledRelease?`
  (`null` = fresh database, `Name == ""` = the unreleased-plans pseudo-release, `Completed` = its
  `ReleaseDeployed` was journaled) and `GetDeployedPlans` is an **unordered set** — resume is by
  membership, never by position, because a release's plans are only partly ordered (disjoint
  dependency chains can be interleaved either way, and the journal records nothing that pins
  which interleaving a past run used). A journaled plan the bundle's copy of the release lacks is
  `IncompatibleBundleException`. The pseudo-release depends on the bundle's last real release,
  so it is an ordinary link in the chain; nothing may depend on *it*, so a database left at the
  pseudo-release is refused rather than matched to a lineage-starting bundle. A completed
  release is never resumed into, so plans a bundle gained for one are not deployed — test
  databases are single-use. Re-deploying an unchanged bundle is `NothingToDeploy`, not an error.
  Mismatches are `IncompatibleBundleException` (the bundle is well formed but does not fit the
  database's state); `InvalidBundleException` stays for malformed manifests.
- **Transaction order** (`DeploymentEngine`): every deployment opens `PrepareJournal` in a
  transaction committed **alone** (the journal's schema must exist before it can be read, and DDL
  auto-commits on some engines), then **both** journal queries in one transaction (so the resume
  decision comes from one consistent view), then the deployment itself — committed at plan or
  release boundaries per `DeploymentCommitLevel`, with `ReleaseDeployed` always journaled in the
  same transaction as the release's last plan. Neither the prepare nor the read commit is reported
  to `IDeploymentListener`: they are not deployment progress. On failure the open transaction rolls
  back — earlier commits stand. The rollback command parses fully but is not implemented; when it
  is, it cannot be "roll back the open transaction", since rollback will be requestable regardless
  of commit level and earlier boundary commits are already durable — it has to be a compensating
  path running rollback scripts in reverse through the `RollingBack*`/`RolledBack*` slots.
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
