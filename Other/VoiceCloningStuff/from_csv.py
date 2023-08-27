import os
import argparse
import csv

if 'TORTOISE_MODELS_DIR' not in os.environ:
	os.environ['TORTOISE_MODELS_DIR'] = os.path.realpath(os.path.join(os.getcwd(), './models/tortoise/'))

if 'TRANSFORMERS_CACHE' not in os.environ:
	os.environ['TRANSFORMERS_CACHE'] = os.path.realpath(os.path.join(os.getcwd(), './models/transformers/'))

os.environ['PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION'] = 'python'

from utils import *

if __name__ == "__main__":
	args = setup_args(cli=True)

	default_arguments = import_generate_settings()
	parser = argparse.ArgumentParser(allow_abbrev=False)
	# parser.add_argument("--text", default=default_arguments['text'])
	parser.add_argument("--delimiter", default='\n')
	parser.add_argument("--emotion", default='None')
	parser.add_argument("--prompt", default='')
	parser.add_argument("--voice", default=default_arguments['voice'])
	parser.add_argument("--mic_audio", default=default_arguments['mic_audio'])
	parser.add_argument("--voice_latents_chunks", default=default_arguments['voice_latents_chunks'])
	# parser.add_argument("--candidates", default=default_arguments['candidates'])
	parser.add_argument("--seed", default=default_arguments['seed'])
	parser.add_argument("--num_autoregressive_samples", default=default_arguments['num_autoregressive_samples'])
	parser.add_argument("--diffusion_iterations", default=default_arguments['diffusion_iterations'])
	parser.add_argument("--temperature", default=default_arguments['temperature'])
	parser.add_argument("--diffusion_sampler", default=default_arguments['diffusion_sampler'])
	parser.add_argument("--breathing_room", default=default_arguments['breathing_room'])
	parser.add_argument("--cvvp_weight", default=default_arguments['cvvp_weight'])
	parser.add_argument("--top_p", default=default_arguments['top_p'])
	parser.add_argument("--diffusion_temperature", default=default_arguments['diffusion_temperature'])
	parser.add_argument("--length_penalty", default=default_arguments['length_penalty'])
	parser.add_argument("--repetition_penalty", default=default_arguments['repetition_penalty'])
	parser.add_argument("--cond_free_k", default=default_arguments['cond_free_k'])

	parser.add_argument("--csv", default='files-to-generate.csv')

	generate_args, unknown = parser.parse_known_args()
	kwargs = {
		'text': '', # to be used from the CSV
		'delimiter': generate_args.delimiter, # not really used anyway
		'emotion': generate_args.emotion,
		'prompt': generate_args.prompt,
		'voice': generate_args.voice,
		'mic_audio': generate_args.mic_audio,
		'voice_latents_chunks': generate_args.voice_latents_chunks,
		'candidates': 1, # always one; one row in CSV = one file
		'seed': generate_args.seed,
		'num_autoregressive_samples': generate_args.num_autoregressive_samples,
		'diffusion_iterations': generate_args.diffusion_iterations,
		'temperature': generate_args.temperature,
		'diffusion_sampler': generate_args.diffusion_sampler,
		'breathing_room': generate_args.breathing_room,
		'cvvp_weight': generate_args.cvvp_weight,
		'top_p': generate_args.top_p,
		'diffusion_temperature': generate_args.diffusion_temperature,
		'length_penalty': generate_args.length_penalty,
		'repetition_penalty': generate_args.repetition_penalty,
		'cond_free_k': generate_args.cond_free_k,
		'experimentals': default_arguments['experimentals'],
	}

	row_count = 0
	with open(generate_args.csv, newline='') as csv_file:
		rows_reader = csv.reader(csv_file, delimiter='|')
		row_count = sum(1 for row in rows_reader)

	tts = load_tts()

	outdir = f"{args.results_folder}/csv/"
	os.makedirs(outdir, exist_ok=True)

	with open(generate_args.csv, newline='') as csv_file:
		rows_reader = csv.DictReader(csv_file, delimiter='|')
		for row_index, row in enumerate(rows_reader):
			# print(row)
			target_filename = os.path.join(outdir, row['FileName'])
			if not target_filename.endswith('.wav'):
				target_filename += '.wav'

			if os.path.isfile(target_filename):
				print('- ' * 40)
				print(f"Skipping {row_index + 1} / {row_count} | File: '{target_filename}' already exist")
				continue
			
			print('- ' * 40)
			print(f"Generating {row_index + 1} / {row_count} | File: '{target_filename}' ")
			
			kwargs['text'] = row['Text'].strip()

			sample_voice, output_voices, stats = generate(**kwargs)
			output_filename = output_voices[0]

			os.rename(output_filename, target_filename)
