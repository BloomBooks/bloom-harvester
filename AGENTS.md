# Agent notes for bloom-harvester

## Issue tracker

Work is tracked on the Bloom YouTrack, with ticket ids like `BL-12345`; agents reach it through the
`youtrack-api` skill. Branch naming and the rest of the team's conventions are in
`bloom-team-skills/TEAM-AGENTS.md`.

## Building

Get Bloom's assemblies into `lib/dotnet` first, as README.md's "Development Dependencies" says;
the projects compile against them (`src/Directory.Build.props`). `.github/workflows/ci.yml` is
the reference for the build and test commands.
