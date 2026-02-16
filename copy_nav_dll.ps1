$src = 'E:\repos\BloogBot\Exports\Navigation\cmake_build_x64\Release\Navigation.dll'
$targets = Get-ChildItem -Path 'E:\repos\BloogBot\Tests\Navigation.Physics.Tests\bin' -Recurse -Filter 'Navigation.dll'
foreach ($t in $targets) {
    Copy-Item $src $t.FullName -Force
    Write-Host "Copied to: $($t.FullName)"
}
