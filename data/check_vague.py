import json, re

def is_vague(u):
    u = u.strip().lower()
    patterns = [r'^-+\s*lain[-\s]*lain', r'^-+\s*other', r'^lain[-\s]*lain', r'^-+\s*$', r'^\s*$']
    return any(re.match(p, u) for p in patterns)

with open(r'd:\Work\Project\Project_AI\hs-classifier\src\data\btki_national.json', encoding='utf-8') as f:
    data = json.load(f)

vague = [(e['hs8'], e['uraian']) for e in data['data'] if is_vague(e['uraian'])]
print(f'Remaining vague: {len(vague)}')
for hs8, u in vague[:30]:
    print(f'  {hs8}: "{u}"')
