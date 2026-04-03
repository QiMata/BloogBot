# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-03) - `PFS-DOCKER-001` deployment complete

- [x] Split Pathfinding/SceneData service topology and package both services for Docker.
- [x] Deploy the split services on the Linux compose stack with mounted `WWOW_DATA_DIR` volumes.
- [x] Capture runtime evidence that both services are reachable and preloading/serving from `/wwow-data`.
- Completion notes:
  - `pathfinding-service` and `scene-data-service` are now running as separate Linux containers from `docker-compose.vmangos-linux.yml`.
  - Both services publish host ports (`5001`, `5003`) and mount `${WWOW_VMANGOS_DATA_DIR}` read-only at `/wwow-data`.
  - Runtime logs confirm active map preload on Pathfinding and ready scene-slice service startup on SceneData.
