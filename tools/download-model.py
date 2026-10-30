import urllib.request, pathlib, hashlib, json, time
root = pathlib.Path(__file__).resolve().parents[1] / 'assets'
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
url = 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin'
with opener.open(url, timeout=60) as response:
    length = int(response.headers.get('Content-Length', '0'))
    sha = hashlib.sha256()
    total = 0
    with (root / 'ggml-small.bin.download').open('wb') as output:
        while True:
            data = response.read(1024 * 1024)
            if not data: break
            output.write(data); sha.update(data); total += len(data)
            if total % (50*1024*1024) == 0: print(f'{total//1048576} MB', flush=True)
    if length and total != length: raise RuntimeError('Incomplete download')
    if total < 400000000: raise RuntimeError('Invalid model size')
    (root / 'ggml-small.bin.download').replace(root / 'ggml-small.bin')
    (root / 'model-manifest.json').write_text(json.dumps({'source':url,'bytes':total,'sha256':sha.hexdigest()}, indent=2))
    print('Model ready', total, sha.hexdigest(), flush=True)
with opener.open('https://raw.githubusercontent.com/ggml-org/whisper.cpp/master/samples/jfk.wav', timeout=60) as response:
    (root / 'jfk.wav').write_bytes(response.read())

