"""Provision a self-contained offline voice worker for this machine. Requires uv."""
from pathlib import Path
import hashlib,json,platform,shutil,subprocess,urllib.request
root=Path(__file__).resolve().parents[2];tools=root/'scripts/narration';library=root/'Game/Library';dest=root/'Game/Assets/StreamingAssets/Kokoro'
def run(*args):subprocess.run([str(x) for x in args],check=True,cwd=root)
system=platform.system();machine=platform.machine().lower()
if system=='Darwin' and machine=='arm64':target='macos-arm64'
elif system=='Windows' and machine in ('amd64','x86_64'):target='windows-x64'
else:raise SystemExit('This platform currently supports baked narration only. Provision on macOS ARM64 or Windows x64.')
env=library/'KokoroEnv';uv=shutil.which('uv')
if not uv:raise SystemExit('Install uv first: https://docs.astral.sh/uv/')
run(uv,'venv','--python','3.12',env)
python=env/('Scripts/python.exe' if system=='Windows' else 'bin/python')
run(uv,'pip','install','--python',python,'-r',tools/'requirements.txt')
checks=json.loads((tools/'model_checksums.json').read_text());dest.mkdir(parents=True,exist_ok=True)
base='https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.1/'
for name,remote in [('model.onnx','kokoro-v1.0.fp16.onnx'),('voices.bin','voices-v1.0.bin')]:
    path=dest/name
    if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest()!=checks[name]:
        temporary=path.with_suffix('.download');urllib.request.urlretrieve(base+remote,temporary)
        if hashlib.sha256(temporary.read_bytes()).hexdigest()!=checks[name]:raise RuntimeError('Checksum mismatch: '+name)
        temporary.replace(path)
run(python,'-m','PyInstaller','--noconfirm','--clean','--name','archive-voice-worker','--onedir','--distpath',dest/('runtime-'+target),'--workpath',library/'KokoroPackaging','--specpath',library/'KokoroPackaging','--collect-all','kokoro_onnx','--copy-metadata','kokoro-onnx','--collect-all','espeakng_loader','--collect-all','phonemizer',tools/'voice_worker.py')
print('Offline worker ready:',target)
