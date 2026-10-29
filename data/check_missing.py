"""Check what happened to missing codes — find OCR variants"""
from docx import Document
import re

doc = Document(r'd:\Work\Project\Project_AI\docs\26~PMK.010~2022 BTKI 2022.docx')

# Search raw text for these codes
search_patterns = ['8471.30', '8429.5', '8711.2', '2709', '8703.1']

print('=== RAW SEARCH IN ALL TABLES ===')
for t_idx, table in enumerate(doc.tables):
    for r_idx, row in enumerate(table.rows):
        for c_idx, cell in enumerate(row.cells):
            txt = cell.text
            for pat in search_patterns:
                if pat in txt:
                    lines = [l.strip() for l in txt.split('\n') if l.strip()]
                    if any(pat in l for l in lines):
                        print(f'\nTable {t_idx} Row {r_idx} Cell {c_idx} [{pat}]:')
                        for l in lines[:8]:
                            print(f'  "{l}"')
                        break
