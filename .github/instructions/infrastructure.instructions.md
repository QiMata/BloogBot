---
applyTo: "docker/**,compose.yaml,docker-compose*.yml,.github/workflows/*.yml,Directory.Build.props,Directory.Build.targets,.editorconfig"
---

# Infrastructure & build config

Containers, CI, and repo-wide build/style settings — the scaffolding around the
code, not the code itself.

| Path | Role |
|------|------|
| `compose.yaml` | local MariaDB dev database (self-contained, no manual prereqs) |
| `docker-compose.windows.yml`, `docker-compose.vmangos-linux.yml` | VMaNGOS server stack (realmd/mangosd) per OS |
| `docker/{database,linux,windows}/` | DB init scripts, vmangos start scripts, migration markers |
| `.github/workflows/*.yml` | CI build/test (`ci.yml`) |
| `Directory.Build.props` / `Directory.Build.targets` | repo-wide MSBuild props (target framework, shared settings) inherited by every project |
| `.editorconfig` | whitespace/style baseline — a **hard lint gate** (commit `eb147769`) |

## Conventions

- Bring services up via the documented compose files — see
  `docs/local-development.md`. Don't invent ad-hoc `docker run` flows.
- Keep CI in lockstep with `scripts/*`: `ci.yml` should call the same
  `scripts/` wrappers a developer runs locally, so green-local ⇒ green-CI.
- Tree-wide settings (framework, analyzers, common props) belong in
  `Directory.Build.props`, not copied into each `.csproj`.
- Editing `.editorconfig` changes the gate the whole tree is measured against —
  re-run `.\scripts\format.ps1` across the repo afterward so nothing regresses.

## Validate with

```powershell
.\scripts\check.ps1                          # build + lint + fast tests (the gate)
docker compose -f compose.yaml config        # lint compose syntax
```

CI mirrors `scripts/`, so a clean `.\scripts\check.ps1` is the local proxy for CI.

## Do NOT

- Commit secrets to `.env` (it is environment-local, not a config source of truth).
- Hand-edit the MaNGOS MySQL database to back infra changes — server state goes
  through SOAP only (`AGENTS.md` §7).
- Duplicate per-project settings that belong in `Directory.Build.props`.

## See also

- `docs/local-development.md` (compose + local DB), root `AGENTS.md` §5.
- Native builds use **MSBuild v145**, not CMake from the solution — see
  `native.instructions.md`; `CMakeLists.txt`/`CMakePresets.json` are secondary.
