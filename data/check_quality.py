import json

with open(r'd:\Work\Project\Project_AI\data\btki_2022_full.json', encoding='utf-8') as f:
    data = json.load(f)

records = data['data']
lines8 = [r for r in records if r['digits'] == 8]
by_code = {r['formatted_code']: r for r in lines8}

# Sample per chapter
samples = {}
for r in lines8:
    bab = r['bab']
    if bab not in samples:
        samples[bab] = r

print('=== SAMPLE PER KEY BAB ===')
key_babs = ['01','27','30','39','72','84','85','87','89']
for bab in key_babs:
    if bab in samples:
        r = samples[bab]
        print(f'Bab {bab}: {r["formatted_code"]} | {r["uraian_id"][:55]} | BM:{r["bea_masuk_mfn"]}')
    else:
        print(f'Bab {bab}: NOT FOUND')

print()
print('=== VERIFY KEY CODES ===')
test = ['8471.30.20','8471.30.10','8704.10.31','8905.90.90','8429.52.00','8711.20.90','8503.00.90']
for code in test:
    if code in by_code:
        r = by_code[code]
        print(f'OK  {code} | {r["uraian_id"][:65]}')
    else:
        print(f'--- {code} MISSING')

print()
print(f'Total 8-digit lines: {len(lines8)}')
print(f'Total unique bab: {len(samples)}')
