"""Offline Kokoro worker. One bounded request per process; no network or game-thread inference."""
import argparse, hashlib, json, re, time
from pathlib import Path
PROFILE='kokoro-fp16-george-v2'
def spoken_text(title, body):
    return re.sub(r'\s+', ' ', title.strip().rstrip('.!?')+'. '+body.strip()).strip()
def cache_key(text):
    return hashlib.sha256((PROFILE+'\n'+text).encode('utf-8')).hexdigest()
def chunks(text):
    result=[]; pending=''
    for sentence in re.split(r'(?<=[.!?])\s+',text):
        pending=(pending+' '+sentence).strip()
        if len(pending)>=110:result.append(pending);pending=''
    if pending:
        if result and len(pending)<55:result[-1]+=' '+pending
        else:result.append(pending)
    return result
class Voice:
    def __init__(self, model_dir):
        import onnxruntime as rt
        from kokoro_onnx import Kokoro
        opts=rt.SessionOptions();opts.log_severity_level=3;opts.intra_op_num_threads=2;opts.inter_op_num_threads=1
        session=rt.InferenceSession(str(model_dir/'model.onnx'),sess_options=opts,providers=['CPUExecutionProvider'])
        self.model=Kokoro.from_session(session,str(model_dir/'voices.bin'))
    def generate(self,text,output):
        import numpy as np
        import soundfile as sf
        parts=[];rate=24000
        for paragraph in chunks(text):
            audio,rate=self.model.create(paragraph,voice='bm_george',speed=.96,lang='en-gb',sentence_pause=.30)
            assert np.isfinite(audio).all() and len(audio)>0
            # Four milliseconds removes cut-edge clicks without removing consonants.
            n=min(int(rate*.004),len(audio)//2)
            audio[:n]*=np.linspace(0,1,n);audio[-n:]*=np.linspace(1,0,n)
            if parts:parts.append(np.zeros(int(rate*.22),dtype=np.float32))
            parts.append(audio)
        audio=np.concatenate(parts);peak=float(np.abs(audio).max())
        if peak>.92:audio*=.92/peak
        output=Path(output);output.parent.mkdir(parents=True,exist_ok=True)
        temp=output.with_suffix('.partial.wav');sf.write(temp,audio,rate,subtype='PCM_16');temp.replace(output)
        return {'duration_seconds':len(audio)/rate,'chunks':len(chunks(text)),'peak':peak,'profile':PROFILE}
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--models',type=Path,required=True);parser.add_argument('--request',type=Path,required=True)
    args=parser.parse_args();request=json.loads(args.request.read_text(encoding='utf-8-sig'))
    text=request['text'];output=request['output']
    if not isinstance(text,str) or not 1<=len(text)<=10000:raise ValueError('Text must contain 1-10000 characters')
    start=time.perf_counter();voice=Voice(args.models);report=voice.generate(text,output);report['total_seconds']=time.perf_counter()-start
    print(json.dumps(report))
if __name__=='__main__':main()
