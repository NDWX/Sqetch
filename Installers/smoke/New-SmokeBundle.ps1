#!/usr/bin/env pwsh
# Regenerates Installers/smoke/bundle, the directory bundle every smoke test deploys with
# 'sqetch-deploy deploy <bundle> --driver sqlite --sqlite-file smoke.db'. The fixture is committed
# so a smoke test needs no 'sqetch' beside the 'sqetch-deploy' it checks, but it is a generated
# file: rebuild it with this script whenever the manifest format changes, rather than editing it.
#
#   pwsh Installers/smoke/New-SmokeBundle.ps1 [-Sqetch <path to a sqetch executable>]
#
# Without -Sqetch the script publishes Pug.Sqetch.Cli for this machine and uses that. The project is
# built from scratch in a temporary directory with the real CLI, so the fixture is exactly what the
# authoring commands produce.

[CmdletBinding()]
param(
	[string] $Sqetch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = ( Resolve-Path ( Join-Path $PSScriptRoot '../..' ) ).Path
$bundleDir = Join-Path $PSScriptRoot 'bundle'
$scratch = Join-Path ( [IO.Path]::GetTempPath() ) "sqetch-smoke-$( [Guid]::NewGuid().ToString( 'N' ) )"
$null = New-Item -ItemType Directory -Path $scratch

function Invoke-Sqetch
{
	param( [Parameter( Mandatory )] [string[]] $Arguments )

	Write-Host "> sqetch $( $Arguments -join ' ' )"
	& $script:Sqetch @Arguments | Out-Host

	if( $LASTEXITCODE -ne 0 )
	{
		throw "sqetch exited with code $LASTEXITCODE"
	}
}

# Statement files are written LF with a trailing newline, whatever the platform running this is.
function Write-SqlFile
{
	param( [Parameter( Mandatory )] [string] $Path, [Parameter( Mandatory )] [string] $Sql )

	[IO.File]::WriteAllText( $Path, $Sql.Replace( "`r`n", "`n" ).TrimEnd() + "`n" )
}

# The journaling SQL a maintainer could write for SQLite: one append-only row per event. It mirrors
# Pug.Sqetch.Tests.DatabaseDrivers/SqliteJournal.cs, which runs the same shape against the engine.
$table = 'sqetch_journal'

# slot -> the parameters it carries beyond the project every slot has (JournalingSlots.ParameterNames)
$eventSlots = [ordered]@{
	DeployingRelease   = @( 'release', 'description', 'utcTimestamp' )
	DeployingPlan      = @( 'release', 'plan', 'description', 'utcTimestamp' )
	DeployingStep      = @( 'release', 'plan', 'step', 'description', 'utcTimestamp' )
	StepDeployed       = @( 'release', 'plan', 'step', 'description', 'utcTimestamp' )
	PlanDeployed       = @( 'release', 'plan', 'description', 'utcTimestamp' )
	ReleaseDeployed    = @( 'release', 'description', 'utcTimestamp' )
	RollingBackRelease = @( 'release', 'utcTimestamp' )
	RollingBackPlan    = @( 'release', 'plan', 'utcTimestamp' )
	RollingBackStep    = @( 'release', 'plan', 'step', 'utcTimestamp' )
	RolledBackStep     = @( 'release', 'plan', 'step', 'utcTimestamp' )
	RolledBackPlan     = @( 'release', 'plan', 'utcTimestamp' )
	RolledBackRelease  = @( 'release', 'utcTimestamp' )
}

$journaling = [ordered]@{}

# two statements, so a multi-statement slot is part of the fixture; both are idempotent because
# PrepareJournal runs on every deployment
$journaling['PrepareJournal'] = @"
create table if not exists $table (
	id integer primary key autoincrement,
	project text not null,
	slot text not null,
	release text not null,
	plan text,
	step text,
	description text,
	at_utc text not null
)
;;
create index if not exists ${table}_release on $table ( project, release )
"@

$journaling['GetLatestRelease'] = @"
select j.release,
		( select count( * ) from $table c
			where c.project = @project and c.release = j.release and c.slot = 'ReleaseDeployed' )
from $table j
where j.project = @project and j.slot = 'DeployingRelease'
order by j.id desc
limit 1
"@

$journaling['GetDeployedPlans'] = @"
select plan from $table
where project = @project and release = @release and slot = 'PlanDeployed'
"@

foreach( $slot in $eventSlots.Keys )
{
	$columns = @( 'project', 'slot' ) + @( $eventSlots[$slot] | ForEach-Object { $_ -eq 'utcTimestamp' ? 'at_utc' : $_ } )
	$values = @( '@project', "'$slot'" ) + @( $eventSlots[$slot] | ForEach-Object { "@$_" } )
	$journaling[$slot] = "insert into $table ( $( $columns -join ', ' ) ) values ( $( $values -join ', ' ) )"
}

try
{
	if( -not $Sqetch )
	{
		$publishDir = Join-Path $scratch 'sqetch-cli'
		& dotnet publish ( Join-Path $repoRoot 'Pug.Sqetch.Cli/Pug.Sqetch.Cli.csproj' ) -m:1 -nologo -c Release -o $publishDir | Out-Host

		if( $LASTEXITCODE -ne 0 )
		{
			throw "dotnet publish exited with code $LASTEXITCODE"
		}

		$Sqetch = Join-Path $publishDir ( $IsWindows ? 'sqetch.exe' : 'sqetch' )
	}

	$script:Sqetch = $Sqetch
	$project = Join-Path $scratch 'project'
	$null = New-Item -ItemType Directory -Path $project

	Push-Location $project
	try
	{
		Invoke-Sqetch @( 'project', 'user', 'Smoke Test', 'smoke@example.com' )
		Invoke-Sqetch @( 'project', 'init', 'smoke', '-d', 'Fixture for the installer smoke tests' )

		foreach( $slot in $journaling.Keys )
		{
			# --file, not the argument: a statement may begin with '--', which parses as an option
			$file = Join-Path $scratch "$slot.sql"
			Write-SqlFile -Path $file -Sql $journaling[$slot]
			Invoke-Sqetch @( 'journaling', 'set', $slot, '--file', $file )
		}

		Invoke-Sqetch @( 'plan', 'create', '-n', 'create-greetings', '-d', 'Creates the greetings table' )
		Invoke-Sqetch @( 'plan', 'add-step', '-p', 'create-greetings', '-n', 'greetings-table', '-d', 'The greetings table' )

		# an unreleased plan's step scripts live under plans/; assigning it to a release moves them
		$step = Join-Path $project 'plans/create-greetings/steps/greetings-table'
		Write-SqlFile -Path ( Join-Path $step 'deploy.sql' ) -Sql 'create table greetings ( id integer primary key, message text not null )'
		Write-SqlFile -Path ( Join-Path $step 'verify.sql' ) -Sql 'select id, message from greetings'
		Write-SqlFile -Path ( Join-Path $step 'rollback.sql' ) -Sql 'drop table greetings'

		Invoke-Sqetch @( 'release', 'create', '-n', 'smoke-1', '-d', 'The only release of the smoke fixture', '--plans', 'create-greetings' )
		Invoke-Sqetch @( 'release', 'finalize', '-n', 'smoke-1' )

		# a rebuild replaces the fixture rather than merging into it; only ever this exact folder
		if( Test-Path -LiteralPath $bundleDir )
		{
			Remove-Item -Recurse -Force -LiteralPath $bundleDir
		}

		Invoke-Sqetch @( 'bundle', '--finalized', '-t', 'directory', '-o', $bundleDir )
	}
	finally
	{
		Pop-Location
	}
}
finally
{
	Remove-Item -Recurse -Force -LiteralPath $scratch
}
