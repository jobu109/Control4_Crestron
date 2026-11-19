from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
import xml.etree.ElementTree as ET
import sys
sys.path.insert(0,'research/_lua_tools')
from lupa import LuaRuntime
base=Path('control4/TSI_Media_Probe');src=(base/'driver.lua').read_text(encoding='utf-8')
ET.parse(base/'driver.xml')
for newer in [False,True]:
 lua=LuaRuntime(unpack_returned_tuples=True)
 lua.execute('Properties={ ["Protocol ID"]="19",["Proxy ID"]="20",["Report Page"]="1" }; updates={}; calls=0; C4={}; phase="early"; function C4:UpdateProperty(k,v) assert(phase=="late","UpdateProperty forbidden during OnDriverInit"); updates[k]=v end; function C4:GetDeviceData(id,tag) calls=calls+1; return "<config><Screens><Name>BrowseStations</Name><Command>GetStations</Command><password>SECRET</password><URL>https://private.test/secret</URL></Screens></config>" end; print=function() end')
 if newer:lua.execute('function C4:GetDriverConfigInfoById(id,tag) return "<Screens><Name>GetBrowseMenu</Name></Screens>" end')
 lua.execute(src);lua.globals().OnDriverInit();assert lua.globals().calls==0
 lua.globals().OnPropertyChanged('Report Page');assert lua.globals().updates['Report Text'] is None
 lua.globals().phase='late';lua.globals().OnDriverLateInit()
 lua.globals().ExecuteCommand('LUA_ACTION',lua.table_from({'ACTION':'InspectTuneIn'}))
 updates=lua.globals().updates;assert 'complete' in updates['Status'];assert len(updates['Report Text'])<=240
 text=''
 for page in range(1,int(updates['Report Pages'])+1):
  lua.globals().Properties['Report Page']=str(page);lua.globals().OnPropertyChanged('Report Page');text+=updates['Report Text']
 assert 'GetStations' in text and 'SECRET' not in text and 'private.test' not in text
 assert ('GetDriverConfigInfoById=true' in text)==newer
 lua.globals().Properties['Protocol ID']='invalid';before=lua.globals().calls;lua.globals().ExecuteCommand('InspectTuneIn',lua.table());assert lua.globals().calls==before
print('Probe checks passed: XML, old/new API paths, action dispatch, no automatic reads, paging bounds, invalid IDs and private scalar suppression.')
import subprocess, shutil
packager=Path(r'C:/Program Files (x86)/Control4/DriverEditor301/DriverPackager.exe')
validator=packager.with_name('DriverValidator.exe')
subprocess.run([str(packager),str(base.resolve()),str(base.resolve()),'TSI_Media_Probe.c4zproj'],check=True,capture_output=True)
archive=base/'TSI_Media_Probe_v2.c4z'
with ZipFile(archive) as z:
 assert z.read('driver.xml').startswith(b'<devicedata>')
 assert 'www/documentation.rtf' in z.namelist()
result=subprocess.run([str(validator),'-d',str(archive.resolve()),'-v','2'],capture_output=True,text=True,encoding='utf-8',errors='replace')
log=result.stdout+result.stderr
Path('reports/MediaProbe-native-validation-v2.log').write_text(log,encoding='utf-8')
assert 'Ran 41 tests' in log and 'OK' in log and 'FAIL' not in log and 'ERROR' not in log,log
shutil.copyfile(archive,base/'TSI_Media_Probe.c4z')
with ZipFile(base/'TSI_Media_Probe_Editor_v2.zip','w',ZIP_DEFLATED) as z:
 for name in ['TSI_Media_Probe.c4zproj','driver.xml','driver.lua','www/documentation.rtf']:
  z.write(base/name,name)
print('Native Control4 packaging and all41 validator checks passed; Composer package and Driver Editor source bundle ready.')
