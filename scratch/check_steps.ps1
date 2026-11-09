$fs = [System.IO.File]::OpenText('C:/Users/ncort/.gemini/antigravity/brain/073a3f9b-529e-4fd7-9c38-e36fbaf82f8a/.system_generated/logs/transcript_full.jsonl')
while (-not $fs.EndOfStream) {
    $line = $fs.ReadLine()
    if ($line.Contains('"step_index":10346') -or $line.Contains('"step_index":10238') -or $line.Contains('"step_index":10240')) {
        $json = $line | ConvertFrom-Json
        Write-Host "Step $($json.step_index) tools:" ($json.tool_calls | ForEach-Object { $_.name + " -> " + $_.args.Path })
        foreach ($c in $json.tool_calls) {
            if ($c.args.Path -like "*SetupSimulationSceneFlow.cs") {
                [System.IO.File]::WriteAllText("scratch/flow_$($json.step_index).cs", $c.args.Content)
            }
            if ($c.args.Path -like "*HolographicTutorialGuide.cs") {
                [System.IO.File]::WriteAllText("scratch/holo_$($json.step_index).cs", $c.args.Content)
            }
            if ($c.args.Path -like "*TutorialManager.cs") {
                [System.IO.File]::WriteAllText("scratch/tm_$($json.step_index).cs", $c.args.Content)
            }
        }
    }
}
$fs.Close()
Write-Host "Done!"
