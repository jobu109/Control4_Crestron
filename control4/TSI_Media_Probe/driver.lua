-- Read-only DriverWorks capability/configuration probe. No playback or network API.
local report = 'Run Inspect TuneIn from Actions.'
local page_size = 240
local late_ready = false
local function property(name, value)
  if C4 and C4.UpdateProperty then C4:UpdateProperty(name, tostring(value)) end
end
local function show_page()
  local count = math.max(1, math.ceil(#report / page_size))
  local page = math.floor(tonumber((Properties or {})['Report Page']) or 1)
  page = math.max(1, math.min(count, page))
  property('Report Pages', count)
  property('Report Text', report:sub((page - 1) * page_size + 1, page * page_size))
end
local function safe(value)
  return type(value) == 'string' and #value <= 96 and value:match('^[%a_][%w_ .:%-]*$')
    and not value:lower():match('password') and not value:lower():match('token')
    and not value:lower():match('credential') and not value:lower():match('account')
    and not value:lower():match('cookie') and not value:lower():match('auth')
end
local function summarize(xml)
  if type(xml) ~= 'string' or #xml == 0 then return 'empty' end
  if #xml > 262144 then return 'oversized; omitted' end
  local tags, names, seen_tag, seen_name = {}, {}, {}, {}
  for tag in xml:gmatch('<([%a_][%w_:%.%-]*)') do
    if #tags < 32 and safe(tag) and not seen_tag[tag] then tags[#tags + 1] = tag; seen_tag[tag] = true end
  end
  for tag, text in xml:gmatch('<([%a_][%w_:%.%-]*)[^>]*>([^<>]*)</[%w_:%.%-]+>') do
    local key = tag:lower()
    if (key == 'name' or key == 'command' or key == 'screenid') and safe(text) and #names < 24 and not seen_name[text] then
      names[#names + 1] = text; seen_name[text] = true
    end
  end
  return 'tags=[' .. table.concat(tags, ',') .. '] names=[' .. table.concat(names, ',') .. ']'
end
local function read_api(method, id, tag)
  if type(C4[method]) ~= 'function' then return 'unavailable' end
  local ok, value = pcall(C4[method], C4, id, tag)
  if not ok then return 'API error (details omitted)' end
  return summarize(value)
end
local function inspect()
  local lines = {'TSI Media Probe 4.0', 'GetDeviceData=' .. tostring(type(C4.GetDeviceData) == 'function'),
    'GetDriverConfigInfoById=' .. tostring(type(C4.GetDriverConfigInfoById) == 'function')}
  for _, name in ipairs({'Protocol ID', 'Proxy ID'}) do
    local id = tonumber((Properties or {})[name])
    if not id or id < 1 or id > 2147483647 or id ~= math.floor(id) then
      property('Status', 'Invalid ' .. name); return
    end
    lines[#lines + 1] = name .. '=' .. tostring(id)
    for _, tag in ipairs({'proxies', 'capabilities', 'config', 'navigator', 'ui'}) do
      lines[#lines + 1] = 'device.' .. tag .. ': ' .. read_api('GetDeviceData', id, tag)
    end
    if type(C4.GetDriverConfigInfoById) == 'function' then
      for _, tag in ipairs({'navigator', 'ui', 'Screens', 'Tabs', 'commands'}) do
        lines[#lines + 1] = 'config.' .. tag .. ': ' .. read_api('GetDriverConfigInfoById', id, tag)
      end
    end
  end
  report = table.concat(lines, '\n'):sub(1, 16384)
  show_page()
  property('Status', 'Inspection complete; see Report Text/pages or Print Report action')
  print(report)
end
function OnDriverInit()
  late_ready = false
end
function OnDriverLateInit()
  late_ready = true
  property('Status', 'Ready; use Actions > Inspect TuneIn')
end
function OnPropertyChanged(name)
  if late_ready and name == 'Report Page' then show_page() end
end
function ExecuteCommand(command, params)
  if not late_ready then return end
  local action = command == 'LUA_ACTION' and (params or {}).ACTION or command
  if action == 'InspectTuneIn' then inspect()
  elseif action == 'PrintReport' then print(report) end
end
