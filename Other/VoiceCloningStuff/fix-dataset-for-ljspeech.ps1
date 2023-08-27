
$companion = 'Jaethal'

$inputData = Import-Csv -Path ".\PreparingDataset\$companion\my-metadata.csv" -Header "Column1", "Column2" -Delimiter "|"
$outputData = @()

foreach ($row in $inputData) {
    $outputData += [PSCustomObject]@{
        Column1 = $row.Column1 -replace "\.wav$"
        Column2 = $row.Column2
        Column3 = $row.Column2.ToLower()
    }

    $inputWavPath = Join-Path -Path ".\PreparingDataset\$companion\wavs" -ChildPath $row.Column1
    $outputWavPath = Join-Path -Path ".\tts-training\input\$companion\wavs" -ChildPath $row.Column1

    & .\ffmpeg -i $inputWavPath -ac 1 -ar 22050 $outputWavPath -y
}

$outputData | Sort-Object -Property Duration | ConvertTo-Csv -Delimiter '|' -NoTypeInformation -UseQuotes AsNeeded | Select-Object -Skip 1 | Out-File ".\tts-training\input\$companion\metadata.csv"
