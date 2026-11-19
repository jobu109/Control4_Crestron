local originalPrint = print
local output
local function run(fn)
    output = {}
    print = function(value) table.insert(output, tostring(value)) end
    ListPresetsForProgramming = fn
    dofile("tools/Inspect-SpotifyHeavy.lua")
    print = originalPrint
    return table.concat(output, "\n")
end
local result = run(function(current)
    assert(current == "")
    return {{text="Other", value="PRIVATE_OTHER"}, {text="HEAVY", value="preset-42"}}
end)
assert(string.find(result, '"preset-42"', 1, true))
assert(not string.find(result, "PRIVATE_OTHER", 1, true))
local callback
result = run(function(_, done) callback = done end)
assert(string.find(result, "waiting", 1, true))
print = function(value) table.insert(output, tostring(value)) end
callback({"HEAVY"})
callback({{text="HEAVY", value="SHOULD_NOT_REPEAT"}})
print = originalPrint
assert(not string.find(table.concat(output), "SHOULD_NOT_REPEAT", 1, true))
result = run(nil)
assert(string.find(result, "not available", 1, true))
result = run(function() error("PRIVATE_ERROR") end)
assert(not string.find(result, "PRIVATE_ERROR", 1, true))
originalPrint("Spotify read-only diagnostic checks passed: exact match, selector output, unrelated-entry suppression, deferred callback, duplicate callback and errors.")
