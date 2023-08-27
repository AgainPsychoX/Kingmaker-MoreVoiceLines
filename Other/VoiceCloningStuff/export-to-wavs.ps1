
function ShouldProcess($file) {
	return $file.ShortName -imatch "Jaethal"
}

$basePath = "G:\Pathfinder Kingmaker\Kingmaker_Data\StreamingAssets\Audio\GeneratedSoundBanks\Windows"
$xml = [XML](Get-Content (Join-Path -Path $basePath -ChildPath "SoundbanksInfo.xml"))
$totalFiles = ($xml.SoundBanksInfo.StreamedFiles.File | Where-Object { ShouldProcess($_) }).Count
$processedFiles = 0

foreach ($file in $xml.SoundBanksInfo.StreamedFiles.File) {
	if (ShouldProcess($file)) {
		$wemPath = Join-Path -Path $basePath -ChildPath "$($file.Id).wem"
		$wavPath = $file.ShortName

		$wavDirectory = Split-Path -Path $wavPath
		New-Item -Path $wavDirectory -ItemType Directory -Force | Out-Null

		Start-Process -FilePath "vgmstream-win64\vgmstream-cli.exe" -ArgumentList "-o ""$wavPath"" ""$wemPath""" -WorkingDirectory $pwd -Wait -NoNewWindow

		Write-Host "finished output $wavPath"

		$processedFiles++
		Write-Host "done $processedFiles / $totalFiles"
	}
}

