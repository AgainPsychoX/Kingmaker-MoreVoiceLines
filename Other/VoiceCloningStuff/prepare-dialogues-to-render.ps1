
# {n}...{/n} = narrator
# {name} = player name
# {race} = player race
# {kingdomname} = kingdom name
# {mf|King|Queen}, or {mf|barony|barony|kingdom|kingdom} = male/famale  + barony/kingdom conditional
# {mf_HighPriest|Priest|Priestess} = ... ffs why it has to be so complicated xD
# {g|Thorn River}...{/g} = location/person/etc?
# {d|Ekun Feast Ok}...{/d} = quest?
# Full list at Kingmaker.TextTools.TextTemplateEngine

# Idea:
# 1. Split to sentences, mark as $uuid_0...N
# 2. Create script to generate the audio(s)
# 3. Create metadata "recipe" about how to play parts (list of 'pX', 'n', 'dX')
# 4. Run the generation process
# 5. Download files from the server
# 8. Consolidation process using generated audio and metadata OR see step 9
# 7. Automated pre-quality-control using Whisper AI (run on the files, even locally, and compare by similarity, maybe detect repetition somehow)
# 8. Manual quality-control
# 9. Consolidation as part of audio player using the recipe

# Recipe elements (string joined by '+'):
# + `pX`, i.e. `p1` - uses file ${uuid}_${i} for voice line
# + `dX`, i.e. `d1000` - delay in ms (from narrator)
# + `n` - name
# + `r` - race
# + `k` - kingdnom name
# + `sX` - uses ${uuid}_${i}_${x} where $x is 'f', 'm' (female/male)
# + `eX' - uses ${uuid}_${i}_${x} where $x is 'b' or 'k' (barony/kingdom)
# + `xX' - uses ${uuid}_${i}_${x} where $x is 'fb', 'mb', 'fk', 'mk' (combination of above)

# TODO: implement stuff ;)

# Example raw text:
# {n}Regongar sizes you up.{/n} \"As you wish. But it's the last order you'll give me, {name}. I'm tired of these arguments. I need to find something more interesting to do. And you... you need to find yourself a new General.\"
# \"There, now... Stop crying. I'm alive, alive and well. You know nothing can beat me! Here, meet each other. Nilak, this is {mf|Baron|Baroness} {name}. {mf|He's|She's} something like a new chieftain for me now. {name}, this is Nilak. She... She's the only decent person in my whole lousy tribe!\"
# \""Lady Aldori...\"" {n}Valerie bows respectfully to the {g|Swordlords}swordlord{/g}.{/n} \""Most of those who were to set off for the {g|Stolen Lands}Stolen Lands{/g} have been killed. Those who yet live will require help. Please allow me to join the expedition.\""
# \""I never rush to judgment. But you must at least arrest this merchant, to question him and give him a chance to repent. Think of those who see him in the streets every day - walking free and unpunished for his crimes! The people appeal to you for justice, {mf|baron|baroness|King|Queen}.\""

$companion = 'Jaethal'

$playerCharacterName = 'Elizabeth'
$playerCharacterSex = 'f'
$playerCharacterRace = 'elf'
$playerKingdomName = 'Lailu League'

################################################################################

function NaturalSortObject {
	[CmdletBinding(PositionalBinding=$false)]
	param (
		[Parameter(ValueFromPipeline)]
		$InputObject,

		[Parameter(Position=0)]
		[string]$PropertyOrExpression,

		[switch]$Descending
	)
	begin {
		Add-Type -TypeDefinition @"
using System.Runtime.InteropServices;
using System.Collections;
public static class Shlwapi_StrCmpLogicalW
{
	[DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
	private static extern int StrCmpLogicalW(string psz1, string psz2);

	public class Comparer : IComparer
	{
		int IComparer.Compare(object x, object y)
		{
			return StrCmpLogicalW(x.ToString(), y.ToString());
		}
	}

	public class ReversedComparer : IComparer
	{
		int IComparer.Compare(object x, object y)
		{
			return StrCmpLogicalW(y.ToString(), x.ToString());
		}
	}

	public static TValue[] Sorted<TValue>(string[] keys, TValue[] items)
	{
		System.Array.Sort(keys, items, new Comparer());
		return items;
	}

	public static TValue[] SortedDescending<TValue>(string[] keys, TValue[] items)
	{
		System.Array.Sort(keys, items, new ReversedComparer());
		return items;
	}
}
"@
		$keys = @()		
		$items = @()
	}
	process {
		if ($PropertyOrExpression -is [string]) {
            $keys += ($InputObject | Select-Object -Property $Property)
        } 
		elseif ($PropertyOrExpression -is [scriptblock]) {
            $keys += ($InputObject | Select-Object $PropertyOrExpression)
        }
		else {
			throw 'No property or expression provided'
		}
		$items += $_
	}
	end {
		if ($Descending) {
			return [Shlwapi_StrCmpLogicalW]::SortedDescending($keys, $items)
		}
		else {
			return [Shlwapi_StrCmpLogicalW]::Sorted($keys, $items)
		}
	}
}

################################################################################

$dialoguesWithRecipes = @()

$longestTextToGenerate = ''

Import-Csv -Path ".\dialogues-cues-missing-voice-lines.csv" -Delimiter "|" | ForEach-Object {
	if ($companion -ine $_.Companion) {
		return
	}

	$uuid = $_.LocalizedStringUUID
	$files = @()
	$recipe = '' 

	if ($uuid -eq '52e8c0f9-506f-4446-ae61-9cb054a1b4d9') {
		Write-Host "RAW: $($_.RawText)"
	}

	$text = $_.RawText -replace '{\/?[gd][^}]*}|{\/?m}', ''
	$text = $text -replace '\\n', ''

	$sentences = (Select-String -AllMatches -Input $text -Pattern '\{n\}[^{]*\{\/n\}|[^\s\\".:!?-](?:[^\\".:!?-]|-\S|\.{3})+(?:(?=\\")|\.{3}|[.:!?-]|\s-)').Matches 
		| ForEach-Object {
			if (!$_ -or !$_.Groups) {
				return;
			}
			$_.Groups[0].Value
		}
	if ($sentences -is [string]) {
		$sentences = , $sentences # Encapsulate into array if single result to allow for-loop
	}

	$i = 0;
	for ($j = 0; $j -lt $sentences.Count; $j++) {
		$sentence = $sentences[$j]

		if ($sentence -match '{n[^a]') {
			$recipe += 'd1000+' # delay of 1 second
		}
		else {
			# Collect small sentences together to avoid very small audio files
			while ($j + 1 -lt $sentences.Count) {
				if ($sentence.Length + $sentences[$j + 1].Length -gt 50) {
					break
				}
				if ($sentences[$j + 1] -match '{n[^a]') {
					break
				}

				$sentence += ' ' + $sentences[$j + 1]
				$j += 1
			}

			$sentence = $sentence.Trim('-').Trim()
			
			# Split sentence into pieces and reassemble in smaller parts to avoid too long audio files
			# that can mess with audio generation model, like duplicated or missed words
			$pieces = [Regex]::Split($sentence, '(?<=,|\?|\!|\.\.\.)\s')
			if ($pieces -is [string]) {
				$pieces = , $pieces
			}
			for ($k = 0; $k -lt $pieces.Length; $k++) {
				$piece = $pieces[$k]
				if ($piece.Length -gt 100) {
					# if ($uuid -eq '45146590-9877-42a1-a1aa-a7cb79f6e69b') {
					# 	Write-Host "Piece '$piece' too long"
					# }
					$subpieces = [Regex]::Split($piece, '\s(?=that|who|which|when|unless|while|and|or|but|because|like|however|instead|than|from)')
					# if ($uuid -eq '6722f1bd-5560-4f22-9e8e-b3d4b77ae4dc') {
					# 	Write-Host "Subpieces:"
					# 	foreach ($subpiece in $subpieces) {
					# 		Write-Host "'$subpiece'"
					# 	}
					# }
					$piece, $subpieces = $subpieces
					# if ($uuid -eq '6722f1bd-5560-4f22-9e8e-b3d4b77ae4dc') {
					# 	Write-Host "Starting with '$piece'"
					# }
					foreach ($subpiece in $subpieces) {
						if (($piece.Length + 1 + $subpiece.Length) -le 100) {
							$piece += ' ' + $subpiece
							$null, $subpieces = $subpieces
						}
						else {
							break
						}
					}
					# if ($uuid -eq '6722f1bd-5560-4f22-9e8e-b3d4b77ae4dc') {
					# 	Write-Host "Cut after '$piece'"
					# }
					if ($k -gt 0) {
						$newPieces = $pieces[0 .. ($k - 1)]
					}
					else {
						$newPieces = @()
					}
					$newPieces += $piece
					if ($subpieces -ne $null) {
						if ($subpieces -is [string]) {
							$subpieces = , $subpieces
						}
						$newPieces += $subpieces[0 .. ($subpieces.Count)]
					}
					$newPieces += $pieces[($k + 1) .. ($pieces.Count)]
					$pieces = $newPieces
					# if ($uuid -eq '6722f1bd-5560-4f22-9e8e-b3d4b77ae4dc') {
					# 	Write-Host "All pieces after replacing the piece:"
					# 	foreach ($piece in $pieces) {
					# 		Write-Host "'$piece'"
					# 	}
					# 	Write-Host "----------------------------"
					# }
				}
			}
			while ($true) {
				$part, $pieces = $pieces
				foreach ($piece in $pieces) {
					if (($part.Length + $piece.Length) -le 80) {
						$part += ' ' + $piece
						$null, $pieces = $pieces
					}
					else {
						break
					}
				}
				if (!$part) {
					break
				}

				if ($uuid -eq '52e8c0f9-506f-4446-ae61-9cb054a1b4d9') {
					Write-Host "PART: $part"
				}

				# Bugfix for missing {mf...}
				# if (!($part -match '{mf')) {
				# 	$i += 1
				# 	continue
				# }
				
				# TODO: Make it universal for players?
				$mfGroup = if ($playerCharacterSex -eq 'm') { '$1' } else { '$2' }
				$part = $part -replace '{name}', $playerCharacterName
				$part = $part -replace '{mf[^|}]*\|([^|}]*)\|([^|}]*)}', $mfGroup
				$part = $part -replace '{race}', $playerCharacterRace
				$part = $part -replace '{kingdomname}', $playerKingdomName

				if ($part -match '{mf\|([^|}]*)\|([^|}]*)\|([^|}]*)\|([^|}]*)}') {
					$mfbGroup = if ($playerCharacterSex -eq 'm') { '$1' } else { '$2' }
					$mfkGroup = if ($playerCharacterSex -eq 'm') { '$3' } else { '$4' }
					$baronySpecific = $part -replace '{mf\|([^|}]*)\|([^|}]*)\|([^|}]*)\|([^|}]*)}', $mfbGroup
					$kingdomSpecific = $part -replace '{mf\|([^|}]*)\|([^|}]*)\|([^|}]*)\|([^|}]*)}', $mfkGroup

					$recipe += "r$i+"

					if ($longestTextToGenerate.Length -lt $baronySpecific.Length) {
						$longestTextToGenerate = $baronySpecific
					}
					if ($longestTextToGenerate.Length -lt $kingdomSpecific.Length) {
						$longestTextToGenerate = $kingdomSpecific
					}

					$files += [PSCustomObject]@{ FileName = "${uuid}_${i}_b.wav"; Text = $baronySpecific };
					$files += [PSCustomObject]@{ FileName = "${uuid}_${i}_k.wav"; Text = $kingdomSpecific };
				}
				else {
					$recipe += "p$i+"

					if ($longestTextToGenerate.Length -lt $part.Length) {
						$longestTextToGenerate = $part
					}

					$files += [PSCustomObject]@{
						FileName = "${uuid}_${i}.wav"
						Text = $part
					};
				}

				$i += 1
			}
		}
	}

	$dialoguesWithRecipes += [PSCustomObject]@{
		LocalizedStringUUID = $uuid
		Companion = $_.Companion
		Recipe = $recipe.Trim('+')
		RawText = $text
	}

	return $files
}
	| Select-Object 'FileName', 'Text'
	| NaturalSortObject -Property 'FileName'
	| ConvertTo-Csv -Delimiter '|' -NoTypeInformation -UseQuotes AsNeeded 
	| Out-File '.\files-to-generate.csv'

$dialoguesWithRecipes
	| Select-Object 'LocalizedStringUUID', 'Companion', 'Recipe', 'RawText'
	| NaturalSortObject -Property 'LocalizedStringUUID'
	| ConvertTo-Csv -Delimiter '|' -NoTypeInformation -UseQuotes AsNeeded 
	| Out-File '.\audio_metadata.csv'

Write-Host "Longest text to generate: ($($longestTextToGenerate.Length)) $longestTextToGenerate"
