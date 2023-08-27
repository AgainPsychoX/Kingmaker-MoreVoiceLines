
$companion = 'Jaethal'
$extraRootDialogues = @(
	'Blueprint:f2bb068a80e3c50499c4478b399cdee7:Jaethal_PartingWords_dialog'
)

################################################################################
# Resolving localized strings

$localizationFiles = Get-Item ".\LocalizationStrings\*.json"

function ResolveLocalizedString {
	param (
		[string]$UUID
	)

	$found = ($localizationFiles | Select-String $UUID)
	if (!$found) {
		return
	}
	if ($found.Count -gt 1) {
		Write-Host "Warning: Indecisive localization string source for '$(UUID)', using first one"
	}
	$localizationFilePath = $found[0].Path

	$textRaw = (Get-Content -Path $localizationFilePath -Raw 
		| Select-String -Pattern """Key"":\s*""$UUID"",\s*""Value"":\s*""(.*)""").Matches[0].Groups[1].Value
	return $textRaw.replace('—', '-').replace('–', '-').replace('…', '...').Trim()
}

################################################################################
# Common stuff

# UUIDs of colleccted LocalizedStrings
$collectedUUIDs = New-Object System.Collections.Generic.HashSet[string]

$unitBlueprintNames = @()
$rootDialogues = New-Object System.Collections.Generic.HashSet[string]
Get-Item ".\BlueprintsDump\Kingmaker.Blueprints.BlueprintUnit\*$companion*.json" | Where-Object {
	$content = Get-Content -Raw $_ | ConvertFrom-Json -AsHashTable
	if ($content.LocalizedName.String.Split(':')[2] -ieq $companion) {
		$unitBlueprintNames += $content.name

		foreach ($component in $content.Components) {
			if ($component.Dialog) {
				$rootDialogues.Add($component.Dialog) | Out-Null
			}
		}
	}
}

# Write header to the file (overwriting the file)
'LocalizedStringUUID|Companion|RawText' | Out-File '.\dialogues.csv'

################################################################################
# Crawl though root dialogues (turn-based most likely, no speaker defined)

$rootDialogues += $extraRootDialogues

$visitedDialogParts = New-Object System.Collections.Generic.HashSet[string]

function HandleAnswer {
	param (
		[Parameter(ValueFromPipeline)][string]$Id,
		[int]$CurrentDepth = 0
	)

	process {
		$fileName = "$($Id.Split(':')[2]).$($Id.Split(':')[1]).json"
		$path = ".\BlueprintsDump\Kingmaker.DialogSystem.Blueprints.BlueprintAnswer\$fileName"
		
		if ($visitedDialogParts.Add($Id)) {
			Write-Host "$("  " * $CurrentDepth)$path"
			$content = Get-Content -Raw $path | ConvertFrom-Json

			$content.NextCue.Cues | HandleCue -CurrentDepth ($CurrentDepth + 1) | Write-Output
		}
		else {
			Write-Host "$("  " * $CurrentDepth)$path (already visited)"
		}
	}
}

function HandleAnswerList {
	param (
		[Parameter(ValueFromPipeline)][string]$Id,
		[int]$CurrentDepth = 0
	)

	process {
		$fileName = "$($Id.Split(':')[2]).$($Id.Split(':')[1]).json"
		$path = ".\BlueprintsDump\Kingmaker.DialogSystem.Blueprints.BlueprintAnswersList\$fileName"
		
		if ($visitedDialogParts.Add($Id)) {
			Write-Host "$("  " * $CurrentDepth)$path"
			$content = Get-Content -Raw $path | ConvertFrom-Json

			$content.Answers | HandleAnswer -CurrentDepth ($CurrentDepth + 1) | Write-Output
		}
		else {
			Write-Host "$("  " * $CurrentDepth)$path (already visited)"
		}
	}
}

function HandleCue {
	param (
		[Parameter(ValueFromPipeline)][string]$Id,
		[int]$CurrentDepth = 0
	)

	process {
		$fileName = "$($Id.Split(':')[2]).$($Id.Split(':')[1]).json"
		$path = ".\BlueprintsDump\Kingmaker.DialogSystem.Blueprints.BlueprintCue\$fileName"
		
		if ($visitedDialogParts.Add($Id)) {
			Write-Host "$("  " * $CurrentDepth)$path"
			$content = Get-Content -Raw $path | ConvertFrom-Json

			if (!$content.Speaker.Blueprint -or $content.Speaker.Blueprint -imatch ($unitBlueprintNames -Join '|')) {
				$uuid = $content.Text.Split(':')[1] 
				$uuid | Write-Output
			}	
			else {
				Write-Host "Warning: Turn dialog with companion includes foregin speaker"
			}

			$shouldBeDone = $false
			if ($content.Answers -and $content.Answers.Count -gt 0) {
				$shouldBeDone = $true
				$content.Answers | HandleAnswerList -CurrentDepth ($CurrentDepth + 1) | Write-Output
			}
			if ($content.Continue.Cues) {
				if ($shouldBeDone) {
					Write-Host "Warning: Both continuation cues and answers detected?"
				}
				$content.Continue.Cues | HandleCue -CurrentDepth ($CurrentDepth + 1) | Write-Output
			}
		}
		else {
			Write-Host "$("  " * $CurrentDepth)$path (already visited)"
		}
	}
}

$i = 0
$rootDialogues | ForEach-Object {
	$fileName = "$($_.Split(':')[2]).$($_.Split(':')[1]).json"
	$path = ".\BlueprintsDump\Kingmaker.DialogSystem.Blueprints.BlueprintDialog\$fileName"
	Write-Host "$path"

	$content = Get-Content -Raw $path | ConvertFrom-Json

	if (!$content.TurnFirstSpeaker) {
		Write-Host "Warning: Dialog TurnFirstSpeaker = false"
	}
	if (!$content.TurnPlayer) {
		Write-Host "Warning: Dialog TurnPlayer = false"
	}

	foreach ($uuid in ($content.FirstCue.Cues | HandleCue -CurrentDepth 1)) {
		if (!$collectedUUIDs.Add($uuid)) {
			Continue
		}

		[PSCustomObject]@{
			LocalizedStringUUID = $uuid
			Companion = $companion
			RawText = (ResolveLocalizedString -UUID $uuid)
		} | Write-Output

		$i += 1
	}
}
	| Select-Object 'LocalizedStringUUID', 'Companion', 'RawText'
	| ConvertTo-Csv -Delimiter '|' -NoTypeInformation -UseQuotes AsNeeded 
	| Select-Object -Skip 1 # header
	# | Out-File '.\dialogues.csv'
	| Add-Content '.\dialogues.csv'

Write-Host "Found $i localized strings from root dialogs (unit/turn-based)"

################################################################################
# Find other cues (most likely multi-speaker dialogues)

$companionSpeakerSelection = Get-Item ".\BlueprintsDump\Kingmaker.DialogSystem.Blueprints.BlueprintCue\*.json"
	| Get-Content -Raw # required for multi-line matching
	| Select-String """\`$type"":\s*""[^""]*DialogSpeaker[^""]*"",\s*""Blueprint"":\s*""Blueprint:.*:(?:$($unitBlueprintNames -Join '|'))"""

Write-Host "Speaker-specified dialog cues to be processed: $($companionSpeakerSelection.Count)"
$i = 0

$companionSpeakerSelection | ForEach-Object {
	$i += 1
	Write-Host "$i / $($companionSpeakerSelection.Count)"

	# $local:name = $_.Matches[0].Groups[1].Value
	$local:uuid = ($_.Line | Select-String -Pattern '"Text": "LocalizedString:([^:]*):')[0].Matches[0].Groups[1].Value
	if (!$local:uuid) {
		return
	}
	if (!$collectedUUIDs.Add($uuid)) {
		return
	}

	return [PSCustomObject]@{
		LocalizedStringUUID = $uuid
		Companion = $companion
		RawText = (ResolveLocalizedString -UUID $uuid)
	}
}
	| Select-Object 'LocalizedStringUUID', 'Companion', 'RawText'
	| ConvertTo-Csv -Delimiter '|' -NoTypeInformation -UseQuotes AsNeeded 
	| Select-Object -Skip 1 # header
	| Add-Content '.\dialogues.csv'

Write-Host "Found $i localized strings from other dialogs (by speaker field)"

# TODO: check duplicates?
# Get-Content .\dialogues.csv | Group-Object | Where-Object { $_.Count -gt 1 } | Select -ExpandProperty Name
