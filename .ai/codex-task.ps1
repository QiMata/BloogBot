param(
    [Parameter(Mandatory=$true)]
    [string]$Prompt
)
# Codex implementation wrapper: runs tasks with full automation.
# Does NOT use yolo/dangerously-bypass flags.
codex exec --full-auto --cd . $Prompt
