-- Paste into the Spotify driver's Lua command area in Composer Pro.
-- Read-only: calls only the advertised custom-selector list function.
-- Does not select a source/preset or change driver properties.
do
    local fn = ListPresetsForProgramming
    local finished = false
    local function emit(list)
        if finished then return end
        finished = true
        if type(list) ~= "table" then
            print("Spotify preset inspection: no list returned; type=" .. type(list))
            return
        end
        local visited, matches = 0, 0
        for _, item in pairs(list) do
            visited = visited + 1
            if visited > 256 then
                print("Spotify preset inspection: stopped at 256 entries")
                break
            end
            local label, value
            if type(item) == "string" then
                label, value = item, item
            elseif type(item) == "table" then
                label, value = item.text, item.value
            end
            if type(label) == "string" and
                string.upper((string.gsub(label, "^%s*(.-)%s*$", "%1"))) == "HEAVY" then
                matches = matches + 1
                print("Spotify preset match: HEAVY")
                if type(value) == "string" and #value <= 256 and
                    not string.find(value, "[%c]") then
                    print("Preset selector string=" .. string.format("%q", value))
                else
                    print("Preset selector not exported; type=" .. type(value))
                end
                if type(item) == "table" then
                    print("Folder=" .. tostring(item.folder == true))
                    print("Selectable=" .. tostring(item.selectable ~= false))
                end
            end
        end
        print("Spotify preset inspection: HEAVY matches=" .. matches)
    end
    if type(fn) ~= "function" then
        print("ListPresetsForProgramming is not available in this driver's Lua context")
    else
        local ok, list = pcall(fn, "", emit)
        if not ok then
            print("Spotify preset inspection: function failed; error content suppressed")
        elseif list ~= nil then
            emit(list)
        elseif not finished then
            print("Spotify preset inspection: waiting for deferred list callback")
        end
    end
end
