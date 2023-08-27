
# AI voice generating for Pathfinder Kingmaker

It was a wild ride for me, and tips/scripts here are rather vague, but maybe someone (or future myself) find that useful.



### Exporting from game files

Assets required:
+ original audio files (duh)
+ game blueprints dump: https://github.com/xADDBx/KingmakerDataminer/releases/
+ `LocalizedStrings` directory copied from `StreamingAssets`.

Use `export-to-wavs.ps1` to convert WEM files for given character as WAVs from `%GAME_DIR%\Kingmaker_Data\StreamingAssets\Audio\GeneratedSoundBanks\Windows`, using [`vgmstream`](https://github.com/vgmstream/vgmstream/releases).

### Filter useful files 

```bash
# Too short to be useful? except few
S:\PathfinderKingmakerVoices\Voices\English\Companions
S:\PathfinderKingmakerVoices\Voices\English\Companions\Jaethal
S:\PathfinderKingmakerVoices\Voices\English\Companions\Jaethal\Asks 
S:\PathfinderKingmakerVoices\Voices\English\Companions\Jaethal\CombatShouts
S:\PathfinderKingmakerVoices\Voices\English\Companions\Jaethal\Asks\Whisper

# Requires manual revision
S:\PathfinderKingmakerVoices\Voices\English\Scenes\Chapter_6
S:\PathfinderKingmakerVoices\Voices\English\Scenes\Chapter_6\CH6_TOS_JaethalTristian
S:\PathfinderKingmakerVoices\Voices\English\Scenes\Chapter_7
S:\PathfinderKingmakerVoices\Voices\English\Scenes\Chapter_7\CH7_PartingWords_Jaethal
S:\PathfinderKingmakerVoices\Voices\English\Scenes\Prologue
S:\PathfinderKingmakerVoices\Voices\English\Scenes\Prologue\Prologue_Jaethal

# Limit to only specific character
S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Amiri
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Ekundayo
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Harrim
S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Jaethal
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Jubilost
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Linzi
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\NokNok
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Octavia
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Regongar
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Tristian
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Twins
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Valerie
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Amiri\Part2
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Ekundayo\DLC1
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Ekundayo\Part2
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Harrim\Part2
S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Jaethal\Jaethal
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Jubilost\Part2
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Linzi\Part_01
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Linzi\Part2
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Octavia\Part2
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Regongar\Part2
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Tristian\Part2
# S:\PathfinderKingmakerVoices\Voices\English\Scenes\Banters\Valerie\Part2
```

I prepared myself `pre-prepare-voices-flat.ps1` to get all interesting audio files and create some metadata file. Still requires manual revision: play all the audio, while reading the metadata line by line looking for mistakes - even little ones like extra "the", missing "good" make a lot of impact. Of course, keep single character voices, for example `Voices\English\Scenes\Chapter_6\*jaethal*` have mixed voices from Jaethal, Tristian and Nyrissa. Also, filter out any accents, character mimic-ing/mocking others, laughter, sights etc. We need clean input, you know, "what you see is what you get" or "garbage in, garbage out".

Then, the samples need to be cut. I did a lot of cutting manually to get best result, but it got to boring, so I stopped in middle - but fortunately I the TTS tool I used came with Speech-To-Text and auto-cutting tool which - while not perfect - was great help

### Text-To-Speech / Speech-To-Text

At first I tried using raw Tortoise TTS and Fast Tortoise TTS, but it took too long for not-so-great results - but it was because I use my local GPU (RTX 2060 Laptop). I read a lot about Coqui TTS (whole framework with multiple models) but it feels "too big". Shortly after I stumbled upon [Tortoise TTS based model with WebUI by mrq](https://git.ecker.tech/mrq/ai-voice-cloning), following tutorial [by Jarods Journey on YouTube](https://www.youtube.com/watch?v=6sTsqSQYIzs). It was easy enough to train and use, even training locally for few hours I got some results.

It also includes Speech-To-Text and cutting utility, which I used to prepare dataset using the voices, and later cross-validated it with the metadata I prepared earlier using `compare-transcribed.ps1`, and I went fixing prepared by the WebUI tool the  `whisper.json` and `train.txt` files.

### Fine-tunning

To fine-tune the model (and later also generate all the new voice lines) in reasonable time, which provides much better results cloning voice than basic zero-shot approach, I rented GPUs at [Vast.AI](https://vast.ai/). 

For Jaethal model fine-tunning, as far I remember, with around 400 audio files each around 10s, I used cosine annealing with 2 or 4 resets, 100 epochs, saving/validating every 10 or 20; taking up to few hours.

### List of files to generate

To get list of missing voice lines I prepared bunch of scripts: 
+ `export-dialogues.ps1` - traverses game blueprint dump JSONs to look for all dialogues related to given character, dumping & resolving localized strings.
+ `remove-dialogues-already-voiced.ps1` - to remove already voiced dialogues by naive cross-referencing metadata, so might require manual tweaking.
+ `prepare-dialogues-to-render.ps1` - fills variables in the dialogues, generates list of files to generate and metadata about them (used to play or join them).

Dialogue parts in the game:
+ Camping banters (already covered by base game)
+ Asks/Barks (almost already covered by most companion, only 2 lines related to rain/snow left)
+ Dialogues
	+ When speaker is defined in blueprint cue, it's trivial.
	+ Some dialogues use 1-on-1 turn system (player-NPC), speaker is not defined in most dialog cues in such cases. 

		Need to look for champion unit blueprint, like `S:\PathfinderKingmakerVoices\BlueprintsDump\Kingmaker.Blueprints.BlueprintUnit\Jaethal_Capital.9b0c830e3643bf8459eb1df4957a525e.json` and look for `Kingmaker.UnitLogic.Interaction.DialogOnClick`, like `Jaethal_Companion_Dialog`.

		Example: `S:\PathfinderKingmakerVoices\BlueprintsDump\Kingmaker.DialogSystem.Blueprints.BlueprintCue\Cue_0066.5a0b39541577e504b9da7080e4ac6c89.json`
		```

+ Cut-scenes (to be researched?)
+ Some odd cases need to be just forced in/out...

### Other scripts

+ `join-voice-files.ps1` - I needed to join files into one to try use some external online tools for voice cloning like Coqui Studio, Resemble AI, etc.
+ `fix-dataset-for-ljspeech.ps1` - Left over script to convert files to mono 22050 Hz for VITS model training, useless for current MRQ's Tortoise TTS approach.

### Plan for generating stuff

1. Buy access on VAST.AI
2. Use `pytorch/pytorch:2.0.1-cuda11.7-cudnn8-devel`
3. Setup port forwarding for `7860`
	```
	docker run -it --gpus all -p 7860:7860 --name test1 pytorch/pytorch:2.0.1-cuda11.7-cudnn8-devel
	```
4. Connect via SSH
5. Update stuff a bit
	```bash
	export DEBIAN_FRONTEND=noninteractive ; export TZ=Europe/Warsaw
	apt-get update ; apt-get install -y tzdata screen curl wget git vim libgl1 libglib2.0-0 libsm6 libxrender1 libxext6
	python3 -m pip install --upgrade pip
	pip3 uninstall -y torch torchvision torchaudio
	pip3 install torch torchvision torchaudio --extra-index-url https://download.pytorch.org/whl/cu118
	```
6.  Open `screen` util (`screen -S name` to start, CTRL+A -> CTRL+D to detach, `screen -list` to list, `screen -r name` re-attach)
7.  Download and setup the repo
	```bash
	git clone https://git.ecker.tech/mrq/ai-voice-cloning ; cd ai-voice-cloning
	git submodule init ; git submodule update --remote
	pip3 install -r ./modules/tortoise-tts/requirements.txt ; pip3 install -e ./modules/tortoise-tts/
	pip3 install -r ./modules/dlas/requirements.txt ; pip3 install -e ./modules/dlas/
	pip3 install -r ./requirements.txt
	pip3 install pyyaml
	```
8.  Tweak scripts to NOT use python `venv`, just `vim train.sh` and remove `venv` lines.
9.  [Copy training files](https://vast.ai/docs/cli/commands#copy-)?
	```
	ls training
	vastai copy ./jaethal 6003038:/workspace/ai-voice-cloning/training/jaethal
	```
10. Start: `python3 ./src/main.py --listen 0.0.0.0:7860` (will download even more stuff)
11. Select training training parameters and train.
12. Test train result (play via browser), select best - in your opinion - settings.
	> I belonged to a wealthy family, and held high position on the courts of Iadara, the capital of Kyonin.
	- Jaethal.
	```
	My settings for Jaethal: 240_128_512_1_P_7_4_2
	Model snapshot from step 240 (cosine)
	Samples: 128
	Iterations: 512
	Temperature: 0.8
	Regression type: P
	Penalties: 4 / 4 / 2
	```
13. Copy `from_csv.py` and list of files to generate
	```
	vastai copy from_csv.py 6003038:/workspace/ai-voice-cloning/src
	vastai copy files-to-generate.csv 6003038:/workspace/ai-voice-cloning
	```
14. Run the script to generate all the new voice lines: `python3 ./src/from_csv.py --csv files-to-generate.csv`
15. Copy back generated files and trained model (if you want to play around other day)
	```
	vastai copy 6003038:/workspace/ai-voice-cloning/results/csv ./generated
	vastai copy 6003038:/workspace/ai-voice-cloning/training ./trained
	```
16. [Shutdown](https://vast.ai/docs/cli/commands#stop-instance-)

### Quality control and fixes

I manually listened to everything generated, noting things to fix. The `prepare-dialogues-to-render.ps1` generated metadata sorted just like Windows Explorer window sorts by name, to ease finding stuff; I used copy of `files-to-generate.csv` to note things, remove everything fine, mark files that require fixes: too long silences, sighs, repeated words, wrong articulated pauses etc. - anything really. Some files required straight up redo due to being hard to fix, or were missing for weird reasons too, so I generated some again with different seed. I prepared myself `audacity.ahk` script along with macros on my mouse to automate fixing things as much as possible, but it still took like 10 hours (or 3 days if you are lazy/unable-to-focus like me).

After files being fixed, I started creating the game mod.


