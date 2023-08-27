
& ffmpeg -y -f lavfi -t 1 -i anullsrc=r=48000:cl=stereo -acodec pcm_s16le -ar 48000 silence.wav

$InputFolder = "VoicesJoining"
$ListFilePath = "list_file.tmp"

if (Test-Path $ListFilePath) {
	Remove-Item $ListFilePath
}

Get-ChildItem -Path $InputFolder -Filter *.wav  | ForEach-Object {
	$RelativePath = (Get-Item $_.FullName | Resolve-Path -Relative) -replace "\\","/"
	"file '$RelativePath'" | Out-File -Append -FilePath $ListFilePath
	# "file 'silence.wav'" | Out-File -Append -FilePath $ListFilePath
}

& ffmpeg.exe -safe 0 -y -f concat -i $ListFilePath -c copy output.wav

Remove-Item $ListFilePath

(Get-Item output.wav).Length / 1MB
