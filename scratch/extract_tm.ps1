$logPath = "C:\Users\ncort\.gemini\antigravity\brain\073a3f9b-529e-4fd7-9c38-e36fbaf82f8a\.system_generated\logs\transcript_full.jsonl"
$stream = [System.IO.File]::OpenRead($logPath)
$reader = New-Object System.IO.StreamReader($stream)
while (-not $reader.EndOfStream) {
    $line = $reader.ReadLine()
    if ($line.Contains("Assets/Scripts/TutorialManager.cs") -and $line.Contains("HolographicTutorialGuide")) {
        $json = $line | ConvertFrom-Json
        foreach ($call in $json.tool_calls) {
            if ($call.name -eq "write_file" -and $call.args.Path.Contains("TutorialManager.cs")) {
                [System.IO.File]::WriteAllText("C:\Users\ncort\.gemini\antigravity\brain\073a3f9b-529e-4fd7-9c38-e36fbaf82f8a\scratch\orig_tutorial_manager.cs", $call.args.Content)
                Write-Host "Found and saved orig_tutorial_manager.cs!"
                break
            }
        }
    }
}
$reader.Close()
$stream.Close()
