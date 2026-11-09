$logPath = "C:\Users\ncort\.gemini\antigravity\brain\073a3f9b-529e-4fd7-9c38-e36fbaf82f8a\.system_generated\logs\transcript_full.jsonl"
$stream = [System.IO.File]::OpenRead($logPath)
$reader = New-Object System.IO.StreamReader($stream)
while (-not $reader.EndOfStream) {
    $line = $reader.ReadLine()
    if ($line.Contains('"step_index":10356') -or $line.Contains('"step_index":10360')) {
        $json = $line | ConvertFrom-Json
        if ($json.tool_calls) {
            foreach ($call in $json.tool_calls) {
                Write-Host "Step $($json.step_index): $($call.name) -> $($call.args.Path)"
                if ($call.args.Path -like "*TutorialManager.cs") {
                    [System.IO.File]::WriteAllText("C:\Users\ncort\.gemini\antigravity\brain\073a3f9b-529e-4fd7-9c38-e36fbaf82f8a\scratch\found_tm_old.cs", $call.args.Content)
                    Write-Host "Saved found_tm_old.cs!"
                }
            }
        }
    }
}
$reader.Close()
$stream.Close()
