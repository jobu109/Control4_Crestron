local timer, ticket, watchdog
local ready = false
local snapshot = "{}"

local function publish(value, status)
    if snapshot ~= value then
        snapshot = value
        C4:SetVariable("NOW_PLAYING_SNAPSHOT", value)
    end
    if Properties["Status"] ~= status then C4:UpdateProperty("Status", status) end
end

local function cancel()
    if watchdog then watchdog:Cancel(); watchdog = nil end
    if ticket then
        local pending = ticket
        ticket = nil
        C4:urlCancel(pending)
    end
end

local function read(url, callback)
    ticket = C4:urlGet(url, {}, false, function(id, body, code)
        if id ~= ticket then return end
        ticket = nil
        if watchdog then watchdog:Cancel(); watchdog = nil end
        if code == 200 and type(body) == "string" and #body <= 32768 then
            callback(body)
        else
            callback(nil)
        end
    end)
    watchdog = C4:SetTimer(3000, function()
        watchdog = nil
        cancel()
        callback(nil)
    end)
end

local xmlEntities = {amp = "&", lt = "<", gt = ">", quot = '"', apos = "'"}
local function decodeXml(value)
    return (value:gsub("&([a-z]+);", function(entity) return xmlEntities[entity] end))
end

local function jsonString(value)
    return '"' .. value:gsub('[%z\1-\31\\"]', function(ch)
        if ch == '"' or ch == '\\' then return '\\' .. ch end
        return string.format('\\u%04x', string.byte(ch))
    end) .. '"'
end

local function sourceIds(raw)
    if type(raw) ~= "string" or #raw > 240 then return nil end
    local ids, seen = {}, {}
    for part in (raw .. ","):gmatch("(.-),") do
        local digits = part:match("^%s*(%d+)%s*$")
        local id = digits and tonumber(digits)
        if not id or id < 1 or id > 2147483647 then return nil end
        if not seen[id] then ids[#ids + 1] = id; seen[id] = true end
    end
    return ids
end

local function ipv4(value)
    if type(value) ~= "string" then return nil end
    local a,b,c,d = value:match("^(%d%d?%d?)%.(%d%d?%d?)%.(%d%d?%d?)%.(%d%d?%d?)$")
    if not a or tonumber(a) < 1 or tonumber(a) >= 224 then return nil end
    for _, part in ipairs({a,b,c,d}) do
        if tonumber(part) > 255 or (#part > 1 and part:sub(1, 1) == "0") then return nil end
    end
    return value
end

local states = {play = "playing", playing = "playing", pause = "paused",
    paused = "paused", buffer = "buffering", buffering = "buffering"}

local function field(xml, name)
    return xml:match("<" .. name .. "%s*[^>]*>([^<]*)</" .. name .. ">")
end

local function seconds(raw)
    local ms = raw and tonumber(raw:match("^%s*(%d+)%s*ms%s*$"))
    if ms and ms <= 86400000 then return math.floor(ms / 1000) end
end

local function publishRoku(host, ids, app, media, status)
    local config = '"roku":{"host":' .. jsonString(host) .. ',"sources":[' .. table.concat(ids, ",") .. ']}'
    if not app then
        publish('{"version":1,' .. config .. ',"sources":[]}', status)
        return
    end
    local fields = { '"app":' .. jsonString(app) }
    local state = media and media:match('<player%s+[^>]-state="([^"]+)"')
    if states[state] then
        fields[#fields + 1] = '"state":' .. jsonString(states[state])
        local duration, position = seconds(field(media, "duration")), seconds(field(media, "position"))
        if duration and duration > 0 then
            fields[#fields + 1] = '"duration":' .. duration
            if position and position <= duration then fields[#fields + 1] = '"position":' .. position end
        end
        local title = field(media, "title")
        if title and #title > 0 and #title <= 512 then
            fields[#fields + 1] = '"title":' .. jsonString(decodeXml(title))
        end
    end
    local rows, data = {}, table.concat(fields, ",")
    for _, id in ipairs(ids) do rows[#rows + 1] = '{"source_id":' .. id .. ',' .. data .. '}' end
    publish('{"version":1,' .. config .. ',"sources":[' .. table.concat(rows, ",") .. ']}',
        states[state] and "Roku playback received" or "Roku app received")
end

local function poll()
    if not ready or ticket then return end
    local host, ids = ipv4(Properties["Roku IP"]), sourceIds(Properties["Watch Source IDs"])
    if not host or not ids then
        publish("{}", "Enter Roku IP and comma-separated Watch Source IDs")
        return
    end
    local base = "http://" .. host .. ":8060/query/"
    read(base .. "active-app", function(xml)
        local attrs, app
        if xml then attrs, app = xml:match("<app%s+([^>]*)>([^<]+)</app>") end
        if not app or #app > 256 then publishRoku(host, ids, nil, nil, "Roku app unavailable"); return end
        if attrs:match('type="home"') or app == "Roku" then publishRoku(host, ids, nil, nil, "Roku Home"); return end
        read(base .. "media-player", function(media)
            publishRoku(host, ids, decodeXml(app), media)
        end)
    end)
end

function OnDriverInit()
    C4:AddVariable("NOW_PLAYING_SNAPSHOT", "{}", "STRING", true, false)
end

function OnDriverLateInit()
    ready = true
    snapshot = "{}"
    C4:SetVariable("NOW_PLAYING_SNAPSHOT", snapshot)
    timer = C4:SetTimer(5000, poll, true)
    poll()
end

function OnPropertyChanged(name)
    if ready and (name == "Roku IP" or name == "Watch Source IDs") then
        cancel()
        publish("{}", "Reading Roku")
        poll()
    end
end

function OnDriverDestroyed()
    ready = false
    if timer then timer:Cancel(); timer = nil end
    cancel()
end
