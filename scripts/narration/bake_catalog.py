"""Bake the current catalog as portable, content-addressed fallback narration."""
from pathlib import Path
import json,yaml,time
from voice_worker import Voice,spoken_text,cache_key
root=Path(__file__).resolve().parents[2];assets=root/'Game/Assets';model=Voice(assets/'StreamingAssets/Kokoro')
report=[]
for path in sorted((assets/'_Project/Resources/Codex').glob('0*.asset')):
    doc=yaml.safe_load(path.read_text().split('--- !u!114 &11400000')[1])['MonoBehaviour']
    text=spoken_text(doc['Title'],doc['Text']);output=assets/'StreamingAssets/SpeechCache'/(cache_key(text)+'.wav')
    start=time.perf_counter();result=model.generate(text,output);result.update(id=doc['Id'],text=text,file=output.name,seconds=time.perf_counter()-start);report.append(result)
    print(json.dumps(result),flush=True)
(root/'Game/Library/NarrationPreview/kokoro-polished-report.json').write_text(json.dumps(report,indent=2))
