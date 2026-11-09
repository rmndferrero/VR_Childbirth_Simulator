$logPath = "C:\Users\ncort\.gemini\antigravity\brain\073a3f9b-529e-4fd7-9c38-e36fbaf82f8a\.system_generated\logs\transcript_full.jsonl"
$stream = [System.IO.File]::OpenRead($logPath)
$reader = New-Object System.IO.StreamReader($stream)
$count = 0
while (-not $reader.EndOfStream) {
    $line = $reader.ReadLine()
    if ($line.Contains("TutorialManager.cs")) {
        $json = $line | ConvertFrom-Json
        if ($json.tool_calls) {
            foreach ($call in $json.tool_calls) {
                if ($call.name -in @("write_file", "edit_file", "replace_file_content")) {
                    $count++
                    Write-Host "Found call: $($call.name) at step $($json.step_index)"
                }
            }
        }
    }
}
$reader.Close()
$stream.Close()
Write-Host "Total: $count"
