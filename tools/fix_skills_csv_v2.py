import csv
import io

input_path = 'H:/Babel/Babel_Client/Assets/Data/Skills/skills.csv'
temp_path = 'H:/Babel/tools/skills_new.csv'

# Read as GBK (original encoding)
with open(input_path, encoding='gbk') as f:
    content = f.read()

reader = csv.reader(io.StringIO(content))
rows = list(reader)

headers = rows[0]
level_idx = headers.index('level')
print(f"level_idx={level_idx}")

# Insert maxLevel after level
new_headers = headers[:level_idx+1] + ['maxLevel'] + headers[level_idx+1:]

max_level_map = {
    'divine_finger': '1',
    'meteor': '2',
    'thunder_auto': '1',
    'aftershock': '1',
    'plague': '1',
    'rage': '1',
    'meteor_evolved': '1',
    'berserker_pact': '1',
}

new_rows = [new_headers]
meteor_level2_inserted = False

for row in rows[1:]:
    if not row or (len(row) == 1 and row[0] == ''):
        continue
    skill_id = row[0]
    max_level = max_level_map.get(skill_id, '1')
    new_row = row[:level_idx+1] + [max_level] + row[level_idx+1:]
    new_rows.append(new_row)

    if skill_id == 'meteor' and not meteor_level2_inserted:
        current_level = row[level_idx] if level_idx < len(row) else '1'
        if current_level == '1':
            meteor2_dict = {
                'skillId': 'meteor',
                'skillName': '天降陨石·强化',
                'description': '强化的陨石，伤害与范围提升',
                'iconPath': 'Icons/meteor',
                'triggerType': 'OnClick',
                'cooldown': '2.5',
                'chargeTime': '1',
                'interval': '',
                'chance': '',
                'effectType': 'hit_aoe',
                'damage': '200',
                'damageRatio': '',
                'radius': '3.5',
                'dps': '',
                'duration': '',
                'statName': '',
                'statValue': '',
                'effect2Type': 'dot_aoe',
                'e2Damage': '',
                'e2DamageRatio': '',
                'e2Radius': '3.5',
                'e2Dps': '0',
                'e2Duration': '3',
                'e2StatName': '',
                'e2StatValue': '',
                'effect3Type': '',
                'e3Damage': '',
                'e3DamageRatio': '',
                'e3Radius': '',
                'e3Dps': '',
                'e3Duration': '',
                'e3StatName': '',
                'e3StatValue': '',
                'level': '2',
                'maxLevel': '2',
                'weight': '0',
                'isStarterSkill': 'FALSE',
                'upgradesFrom': '',
            }
            meteor2_row = [meteor2_dict.get(h, '') for h in new_headers]
            new_rows.append(meteor2_row)
            meteor_level2_inserted = True
            print("Inserted meteor level2")

print(f"Total rows: {len(new_rows)}")

# Write as UTF-8
with open(temp_path, 'w', encoding='utf-8', newline='') as f:
    writer = csv.writer(f)
    writer.writerows(new_rows)

print(f"Written to {temp_path}")

# Verify
print("\n--- Verification ---")
with open(temp_path, encoding='utf-8') as f:
    verify_content = f.read()

verify_reader = csv.reader(io.StringIO(verify_content))
verify_rows = list(verify_reader)
vh = verify_rows[0]
li = vh.index('level')
mi = vh.index('maxLevel')
wi = vh.index('weight')

for row in verify_rows:
    if not row or (len(row) == 1 and row[0] == ''):
        continue
    sid = row[0]
    lv = row[li] if li < len(row) else '?'
    ml = row[mi] if mi < len(row) else '?'
    wt = row[wi] if wi < len(row) else '?'
    nm = row[1] if len(row) > 1 else '?'
    print(f"  {sid} | name={nm} | level={lv} | maxLevel={ml} | weight={wt}")
