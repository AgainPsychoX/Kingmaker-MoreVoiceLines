
$companion = 'Jaethal'
$extraRemoveByUUID = @(
	'139aeffb-6524-4bc4-9fbe-a389dab2d689' # transribed text don't match because extra 'good' word
	'8d872d54-5fca-42da-9ae0-b9fb247f4ace' # alright =/= all right 
	'ff374d23-dfa9-4f05-969b-6a18d5fd7c45' # word ordering: we now =/= now we
	'46044566-7bd1-4aa2-9396-311d2649250b' # idk, bark in cutscene? 
)
$ignoreAudioFileNames = @(
	'CH6_jaethalTristianTOS_03_PART-05.wav' # transribed text don't match because extra 'good' word
	'Prologue_Jaethal_03_PART-02.wav' # alright =/= all right 
	'CH6_jaethalTristianTOS_01.wav' # word ordering: we now =/= now we
	'CH6_jaethalTristianTOS_Barks_10.wav' # idk, bark in cutscene? 
)

$totalText = ''

$allDialogues = Import-Csv -Path ".\dialogues.csv" -Delimiter "|" | ForEach-Object {
	if ($companion -ine $_.Companion) {
		return
	}

	$text = $_.RawText -replace '{n}(?:[^{]*{\/n})|{[^}]*}|\\n', ''
	$text = ($text -replace '["''.,:;\-?!\\]|\s', '').ToLower()

	$totalOffset = $totalText.Length
	$totalText += $text

	if ($_.LocalizedStringUUID -eq '7b9f19c9-42ea-4547-a913-7402f1b79bac') {
		Write-Host $text
	}

	return [PSCustomObject]@{
		LocalizedStringUUID = $_.LocalizedStringUUID
		Companion = $_.Companion
		RawText = $_.RawText
		ToBeRemoved = $false
		TotalOffset = $totalOffset
	}
}

foreach ($uuidToRemove in $extraRemoveByUUID) {
	foreach ($dialogue in $allDialogues) {
		if ($dialogue.LocalizedStringUUID -ieq $uuidToRemove) {
			$dialogue.ToBeRemoved = $true
			break
		}
	}
}

& {
	Import-Csv -Path ".\PreparingDataset\Jaethal\my-metadata.csv" -Delimiter '|' -Header 'FileName', 'Text'
	Import-Csv -Path ".\PreparingDataset\Jaethal\whisper-cuts-metadata.csv" -Delimiter '|' -Header 'FileName', 'Text'
} | ForEach-Object {
	if ($_.FileName.StartsWith('Banter_')) {
		return # skip
	}
	if ($_.FileName -imatch '_CantEquip_|_CantCast_|_BattleStart_|_Select_|_Move_|_MoveAlternative_|_AttackOrder_|_CharCrit_|_CheckFailed_|_CheckSuccess_|_Discovery_|_SelectJoke_|_SelectJokeAlternative_') {
		return
	}
	if ($ignoreAudioFileNames.Contains($_.FileName)) {
		return
	}

	$text = $_.Text
	$text = ($text -replace '["''.,:;\-?!\\]|\s', '').ToLower()

	if ($_.FileName -eq 'CH6_jaethalTristianTOS_07_PART-02.wav') {
		Write-Host $text
	}

	$position = $totalText.IndexOf($text)
	if ($position -gt 0) {
		$previous = $null
		foreach ($dialogue in $allDialogues) {
			if ($dialogue.TotalOffset -gt $position) {
				$previous.ToBeRemoved = $true
				if ($_.FileName -eq 'CH6_jaethalTristianTOS_07_PART-02.wav') {
					Write-Host "removed $($previous.LocalizedStringUUID) because $($_)"
				}
				break
			}
			$previous = $dialogue
		}
	}
	else {
		Write-Host "Warning: Dialogue not found for file '$($_.FileName)', text: $($_.Text)"
	}
}

$removedCount = ($allDialogues | Where-Object { $_.ToBeRemoved }).Count
Write-Host "Removed count: $removedCount"
# foreach ($dialogue in $allDialogues) {
# 	if ($dialogue.ToBeRemoved) {
# 		Write-Host "`t${dialogue.LocalizedStringUUID} $($_.RawText)"
# 	}
# }

$allDialogues 
	| Where-Object { !$_.ToBeRemoved }
	| Select-Object 'LocalizedStringUUID', 'Companion', 'RawText'
	| ConvertTo-Csv -Delimiter '|' -NoTypeInformation -UseQuotes AsNeeded 
	| Out-File '.\dialogues-cues-missing-voice-lines.csv'
