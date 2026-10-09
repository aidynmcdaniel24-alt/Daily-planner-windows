# Daily Planner for Windows

The Windows app for [Daily Planner](https://aidynmcdaniel24-alt.github.io/Daily-planner/), built with C# and WinUI 3.
It uses the same account and data as the website.

## Download
Get **DailyPlannerSetup.exe** from the [latest release](https://github.com/aidynmcdaniel24-alt/Daily-planner-windows/releases/latest).

## How it's built
Every push to `main` is built on GitHub's Windows machines (see `.github/workflows/build.yml`)
and published as a new release.

## Project layout
- `src/DailyPlanner/Pages` – the screens (login, home, checklists, settings)
- `src/DailyPlanner/Services` – sign-in, syncing, and the planner data
- `installer/setup.iss` – the installer script

## Building it yourself (optional)
Install Visual Studio 2022 Community with the **WinUI application development** workload,
open `src/DailyPlanner/DailyPlanner.csproj`, pick **x64**, and press F5.
