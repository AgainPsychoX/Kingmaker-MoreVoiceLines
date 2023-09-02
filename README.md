
# More Voice Lines for Pathfinder: Kingmaker

This is modification for [Pathfinder: Kingmaker](https://store.steampowered.com/app/640820/Pathfinder_Kingmaker__Enhanced_Plus_Edition/). Uses [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) and [0Harmony](https://harmony.pardeike.net/). Audio files along with some configuration are required. For now, only Jaethal voice lines are done (at least by me), but you are free to contact me via GitHub Issues or e-mail if you want to contribute more. The repository also includes [notes on voice clonning & scripts](./Other/VoiceCloningStuff/README.md) I used for generating Jaethal.

**You can [download the mod from Nexus Mods](https://www.nexusmods.com/pathfinderkingmaker/mods/292).**



## Notes

+ Voice over requests from game are detected by `LocalizedString.PlayVoiceOver` patch. We map those strings UUIDs to audio files, with special "recipe" that describes which actual files are to be used, in what order. Some suffixes are expected for sex or kingdom status dependent dialogues.
+ Audio files should be placed in `audio` directory inside mod directory. I used AI generated samples with 44100 Hz sample rate single channel (mono), but the mod can also play any other sample rate and stereo (might require tweaking the `AudioPlaybackEngine` instance on audio player side for better performance).
+ Next to the `audio` folder there should be `audio_metadata.csv`, `|` separated file with columns: `LocalizedStringUUID`, `Companion` (unused actually), `Recipe` and `RawText` (also unused). 
+ **Modification uses external executable** (outside the game) for playing the sound, sources are of course included in the repository, as I failed to found another way.

	<details>
	
	+ I couldn't get Unity [`AudioClip`](https://docs.unity3d.com/ScriptReference/AudioClip.html) to work
		
		Neither `UnityWebRequestMultimedia.GetAudioClip` nor `WWW.GetAudioClip` worked for me, maybe because wierd WAV format. They did load the bytes, but did not produce usable `AudioClip` (length was 0). I tried manually [`AudioClip.Create`](https://docs.unity3d.com/ScriptReference/AudioClip.Create.html) and `AudioClip.SetData`, with [custom WAV parsing](https://gist.github.com/AgainPsychoX/e984c2deb6addd2bc2b389b28268e16a). Sadly I always got `AudioClip.GetData failed; AudioClip  contains no data` (on `SetData`), because for some fucking reason `Create`, despite having channels parameter, always creates 0 channles.
		
	+  I tried using C# [`MediaPlayer`](https://learn.microsoft.com/pl-pl/dotnet/api/system.windows.media.mediaplayer) and [`SoundPlayer`](https://learn.microsoft.com/pl-pl/dotnet/api/system.media.soundplayer), but had issues with dependencies (WPF disagrees with Unity). Maybe one day someone (or me) will try using `SoundPlayer` tho.
	+ For now, ended up using separated process with [NAudio](https://github.com/naudio/NAudio) library to playback the audio files, communicates with game process using [named pipes IPC](https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-use-named-pipes-for-network-interprocess-communication), one receiving requests from game, other sending notifcations when audio/recipe finishes.

	</details>


### To-do

+ Add detecting missing dialogues to the mod; save to file and keep showing "notification" on the mod settings asking user to upload their logs/update the mod.
+ Replace dialogues referencing player character name by something more generic (for now it's fixed to use my character Elizabeth).
+ Add pitch setting
+ Add speed setting
+ Add [re-focus](https://stackoverflow.com/questions/25578305/c-sharp-focus-window-of-a-runing-program) option, as sometimes game window doesn't get focus, maybe because conosle window (even if hidden) blinks.
+ Allow skip part of the voice line using space, if possible using speed up


