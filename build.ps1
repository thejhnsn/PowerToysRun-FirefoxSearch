# taken and modified from https://github.com/8LWXpg/PowerToysRun-PluginTemplate 

Push-Location
Set-Location $PSScriptRoot

$name = 'FirefoxSearch'
$assembly = "Community.PowerToys.Run.Plugin.$name"
$version = "v$((Get-Content ./plugin.json | ConvertFrom-Json).Version)"
$archs = @('x64')

Remove-Item ./out/*.zip -Recurse -Force -ErrorAction Ignore
foreach ($arch in $archs) {
	$releasePath = "./bin/$arch/Release/net8.0-windows"

	dotnet build -c Release /p:Platform=$arch --runtime windows-x64
	Remove-Item "./out/$name/*" -Recurse -Force -ErrorAction Ignore
	mkdir "./out/$name" -ErrorAction Ignore
	Copy-Item "$releasePath/$assembly.dll", "$releasePath/plugin.json", "$releasePath/Images", "$releasePath/$assembly.deps.json", "$releasePath/Microsoft.Data.Sqlite.dll" "./out/$name" -Recurse -Force
	Compress-Archive "./out/$name" "./out/$name-$version-$arch.zip" -Force
	Get-FileHash "./out/$name-$version-$arch.zip" -Algorithm SHA256 | Format-List
}

Pop-Location
