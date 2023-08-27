
$companion = 'Jaethal'

function ResolveLocalizedStringFromFile {
	param (
		[PSCustomObject]$LocalizationContent, 
		[string]$Id
	)

	$uuid = $Id # assume it's UUID
	$colonIndex = $Id.IndexOf(':')
	if ($colonIndex -ne -1) {
		$uuidStart = $colonIndex + 1
		$uuid = $Id.Substring($uuidStart, 36)
	}

	$found = ''
	foreach ($pair in $LocalizationContent.strings) {
		if ($pair.Key -eq $uuid) {
			$found = $pair.Value
			break
		}
	}
	if ($found) {
		return $found.replace('—', '-').replace('–', '-').replace('…', '...').Trim().Trim('"').Trim("'")
	}
	throw "Value for localized string not found"
}

$csvData = @()

Get-ChildItem -Path '.\\Voices\\' -Filter *.wav -Recurse | Where-Object {
	$_.FullName -notmatch '\\Banters\\' -or $_.FullName -match "\\Banters\\$companion" 
} | ForEach-Object {
	$note = ""
	if ($_.Name -imatch 'whisper') {
		# $note += "[whisper]"
		return;
	}
	if ($_.Name -imatch '_EnterStealth_') {
		return;
	}
	if ($_.Name -imatch '_Attack_' -or $_.Name -match '_AttackPower_') {
		return;
	}
	if ($_.Name -imatch '_Poisoned_' -or $_.Name -imatch '_Pain_' -or $_.Name -imatch '_Death_') {
		return;
	}
	if ($_.Name -imatch '_Fatigue_' -or $_.Name -imatch '_Unconsious_') {
		return;
	}

	$destinationPath = Join-Path -Path '.\VoicesPrePreparedFlat' -ChildPath $_.Name
	Copy-Item -Path $_.FullName -Destination $destinationPath -Force

	$duration = [double] (& ffprobe.exe -i $_.FullName -show_entries format=duration -v quiet -of csv="p=0")

	$text = ""
	if ($_.Name.StartsWith("Banter_")) {
		$jsonNiceNamePart = $_.BaseName
		$jsonFile = Get-Item ".\BlueprintsDump\Kingmaker.*/$jsonNiceNamePart.*.json"
		while (!$jsonFile) {
			$lastUnderscoreIndex = $jsonNiceNamePart.LastIndexOf('_')
			if ($lastUnderscoreIndex -lt 0) {
				break;
			}
			$jsonNiceNamePart = $jsonNiceNamePart.Substring(0, $lastUnderscoreIndex)
			$jsonFile = Get-Item ".\BlueprintsDump\Kingmaker.BarkBanters.BlueprintBarkBanter\$jsonNiceNamePart.*.json"
		}

		if (($jsonFile) -and ($jsonFile | Measure-Object).Count -eq 1) {
			# Write-Host "Banter file: $jsonFile"
			$jsonContent = Get-Content -Path $jsonFile -Raw | ConvertFrom-Json
			$localizationFile = Get-Item ".\LocalizationStrings\enGB$($jsonContent.m_AssetGuid).json"
			if (!$localizationFile) {
				$note += "[Cannot find localization file, see '$($jsonFile.FullName)']"
			}
			else {
				# Write-Host "Localization file: $localizationFile"
				$localizationContent = Get-Content -Path $localizationFile -Raw | ConvertFrom-Json
				if ($_.Name -imatch 'Response') {
					$matchingResponses = $jsonContent.Responses | Where-Object { $_.Unit -imatch $companion }
					if ($matchingResponses.Count -eq 1) {
						try {
							$text = ResolveLocalizedStringFromFile -LocalizationContent $localizationContent -Id $matchingResponses[0].Response
						}
						catch {
							$note += "[Error: Failed to resolve localized string for response '$($matchingResponses[0].Response)' in file '$localizationFile'. Reason: $($_.Exception.Message)]"
						}
					}
					else {
						$note += "[Error: Indecisive response, see '$($jsonFile.FullName)']"
					}
				}
				else {
					$phrase = ''
					if ($jsonContent.FirstPhrase.Count -eq 1) {
						$phrase = $jsonContent.FirstPhrase[0]
					}
					else {
						$phaseNumberIndex = $_.BaseName.IndexOf('FP');
						if ($phaseNumberIndex -lt 0) {
							$note += "[Error: Indecisive first phrase, see '$($jsonFile.FullName)']"
						}
						$phaseNumberIndex += 2 # skip 'FP' chars
						$index = ([int] $_.BaseName.Substring($phaseNumberIndex))
						$phrase = $jsonContent.FirstPhrase[$index - 1]
					}
					try {
						$text = ResolveLocalizedStringFromFile -LocalizationContent $localizationContent -Id $phrase
					}
					catch {
						$note += "[Error: Failed to resolve localized string for response first phrase '$phrase' in file '$localizationFile'. Reason: $($_.Exception.Message)]"
					}
				}
			}
		}

		if ($text) {
			Write-Host "Handled by barter blueprints: $($_.Name)"
		}
	}

	if ($text -match '\{|\}') {
		$note += "[Contains variable or narrator]"
		Write-Host "Requires variables/narrator fixes: $($_.Name)"
	}
	if (!$text) {
		Write-Host "Requires manual work for $($_.Name)"
	}

	$csvData += [PSCustomObject]@{
		FileName = $_.Name
		Text = $text
		Duration = $duration
		Note = $note
	}
}

$csvData | Sort-Object -Property Duration | ConvertTo-Csv -Delimiter '|' -NoTypeInformation -UseQuotes AsNeeded | Select-Object -Skip 1 | Out-File '.\my-raw-metadata.csv'
