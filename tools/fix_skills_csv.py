import csv
import io
import os

input_path = r'H:/Babel/Babel_Client/Assets/Data/Skills/skills.csv'
output_path = r'H:/Babel/Babel_Client/Assets/Data/Skills/skills.csv'

# Read - try utf-8-sig first, then gbk
try:
    with open(input_path, encoding='utf-8-sig', errors='replace') as f:
        content = f.read()
    print("Read as utf-8-sig")
except Exception as e:
    print(f"utf-8-sig failed: {e}")
    try:
        with open(input_path, encoding='gbk', errors='replace') as f:
            content = f.read()
        print("Read as gbk")
    except Exception as e2:
        print(f"gbk also failed: {e2}")
        raise

reader = csv.reader(io.StringIO(content))
rows = list(reader)

print(f"Total rows: {len(rows)}")
print(f"Headers: {rows[0]}")
print()

headers = rows[0]

# Find column indices
level_idx = headers.index('level')
weight_idx = headers.index('weight')
print(f"level_idx={level_idx}, weight_idx={weight_idx}")

# Insert maxLevel column after level
new_headers = headers[:level_idx+1] + ['maxLevel'] + headers[level_idx+1:]
print(f"New headers: {new_headers}")

# maxLevel map by skillId
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
        # skip empty rows
        continue

    skill_id = row[0]
    # Insert maxLevel value after level column
    max_level = max_level_map.get(skill_id, '1')
    new_row = row[:level_idx+1] + [max_level] + row[level_idx+1:]
    new_rows.append(new_row)

    # After meteor level1 row, insert meteor level2
    if skill_id == 'meteor' and not meteor_level2_inserted:
        current_level = row[level_idx] if level_idx < len(row) else '1'
        print(f"Found meteor row, level={current_level}")
        if current_level == '1':
            # Build meteor level2 row matching new header structure
            # New headers after insert:
            # skillId,skillName,description,iconPath,triggerType,cooldown,chargeTime,interval,chance,
            # effectType,damage,damageRatio,radius,dps,duration,statName,statValue,
            # effect2Type,e2Damage,e2DamageRatio,e2Radius,e2Dps,e2Duration,e2StatName,e2StatValue,
            # effect3Type,e3Damage,e3DamageRatio,e3Radius,e3Dps,e3Duration,e3StatName,e3StatValue,
            # level,maxLevel,weight,isStarterSkill,upgradesFrom

            # Create a dict for easy construction
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
            print("Inserted meteor level2 row")

print(f"\nTotal new rows (incl header): {len(new_rows)}")

# Write to temp first, then replace
temp_path = output_path + '.tmp'
with open(temp_path, 'w', encoding='utf-8', newline='') as f:
    writer = csv.writer(f)
    writer.writerows(new_rows)

print(f"Written to temp {temp_path}")

# Replace original
import shutil
shutil.move(temp_path, output_path)
print(f"Replaced {output_path}")

# Verify
print("\n--- Verification ---")
with open(output_path, encoding='utf-8', errors='replace') as f:
    verify_content = f.read()

verify_reader = csv.reader(io.StringIO(verify_content))
verify_rows = list(verify_reader)
verify_headers = verify_rows[0]

print(f"Headers: {verify_headers}")
level_idx2 = verify_headers.index('level')
max_level_idx = verify_headers.index('maxLevel')
print(f"level_idx={level_idx2}, maxLevel_idx={max_level_idx}")
print()

for row in verify_rows[1:]:
    if not row or (len(row) == 1 and row[0] == ''):
        continue
    sid = row[0]
    lv = row[level_idx2] if level_idx2 < len(row) else '?'
    ml = row[max_level_idx] if max_level_idx < len(row) else '?'
    wt_idx = verify_headers.index('weight')
    wt = row[wt_idx] if wt_idx < len(row) else '?'
    print(f"  skillId={sid}, level={lv}, maxLevel={ml}, weight={wt}")
