import json, os
f = r'd:\Work\Project\Project_AI\hs-classifier\src\data\btki_national.json'
print('File size:', os.path.getsize(f), 'bytes')
with open(f, encoding='utf-8') as fh:
    data = json.load(fh)
entries = data['data']
text = '\n'.join(d['hs8'] + ' - ' + d['uraian'] for d in entries)
approx_tokens = len(text) // 4
print('Entries:', len(entries))
print('Text chars:', len(text))
print('Approx tokens if all injected:', approx_tokens)
print('Groq 8K limit would be exceeded:', approx_tokens > 6000)
