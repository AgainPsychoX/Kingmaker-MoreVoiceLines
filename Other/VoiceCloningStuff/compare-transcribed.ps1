
$metadataCsvPath = ".\PreparingDataset\Jaethal\whisper-cuts-metadata.csv"
$trainCsvPath = ".\ai-voice-cloning\training\jaethal2\train.txt"
$outputFilePath = ".\transcribions-comparsion.txt"

$metadataCsvData = Import-Csv -Path $metadataCsvPath -Delimiter '|' -Header "FileName","ExpectedText"
$trainCsvData = Import-Csv -Path $trainCsvPath -Delimiter '|' -Header "C1","C2" | ForEach-Object {
	[PSCustomObject]@{
		FileName = $_.C1 -replace '^audio/', ''
		ActualText = $_.C2
	}
}

$outputContent = foreach ($record in $metadataCsvData) {
	$matchingTrainRecord = $trainCsvData | Where-Object { $_.FileName -eq $record.FileName }

	if ($matchingTrainRecord) {
		$isSameInASCII = ($record.ExpectedText -replace '[\W]', '').ToUpper() -eq ($matchingTrainRecord.ActualText -replace '[\W]', '').ToUpper()
		$padding = ' ' * ($record.FileName.Length + 1)
		"$($record.FileName)|$($record.ExpectedText)|$(if ($isSameInASCII) {''} else {'(different?)'})`r`n$($padding)$($matchingTrainRecord.ActualText)"
	}
	else {
		Write-Host "Missing '$($record.FileName)'"
	}
}

$outputContent | Out-File -FilePath $outputFilePath
