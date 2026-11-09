$items = Get-ChildItem -Path . -Recurse -Filter "*OculusTouchForQuest2_Right*" -ErrorAction SilentlyContinue
foreach ($it in $items) {
    Write-Host $it.FullName
}
