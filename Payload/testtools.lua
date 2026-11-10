-- Reversible experiment: accept attacks throughout a wide combo timing window.
-- This must be tested in game; it may affect other timing behavior.
local log = io.open("direct_combat_perfect_combo.log", "w")
local ok, err = pcall(function()
    g_cAuroraSettings.m_bUseOldAttackMechanics = true
    g_cAuroraSettings.m_fCombatTimeAdvance = 10.0
end)
if log then
    log:write("perfect combo settings: ", tostring(ok), " ", tostring(err), "\n")
    if ok then
        log:write("old mechanics: ", tostring(g_cAuroraSettings.m_bUseOldAttackMechanics), "\n")
        log:write("combat time advance: ", tostring(g_cAuroraSettings.m_fCombatTimeAdvance), "\n")
    end
    log:close()
end
