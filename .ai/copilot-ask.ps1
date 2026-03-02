param(
    [Parameter(Mandatory=$true)]
    [string]$Prompt
)
# Copilot Ask-mode wrapper: read-only codebase Q&A only.
# Denies: shell execution, file writes, URL fetching, memory storage.
copilot -s --no-ask-user --allow-all-tools --deny-tool 'shell' --deny-tool 'write' --deny-tool 'url' --deny-tool 'memory' -p $Prompt
