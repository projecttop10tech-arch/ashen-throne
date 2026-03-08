/**
 * Unit tests for PlayFab Cloud Script: economy.js
 *
 * These tests mock the PlayFab `server` global to test logic in isolation.
 * Run with: npm test
 */

"use strict";

// Mock PlayFab server global
const mockServer = {
    GetUserInternalData: jest.fn(),
    UpdateUserInternalData: jest.fn(),
    GetUserData: jest.fn(),
    UpdateUserData: jest.fn(),
    GrantItemsToUser: jest.fn(),
    GetUserInventory: jest.fn(),
    ConsumeItem: jest.fn(),
    ValidateIOSReceipt: jest.fn(),
    ValidateGooglePlayPurchase: jest.fn()
};

global.server = mockServer;
global.currentPlayerId = "TEST_PLAYER_001";
global.log = { info: jest.fn(), warning: jest.fn(), error: jest.fn() };
global.handlers = {};

// Load module (sets handlers)
require("../CloudScript/economy.js");

// ─── GrantCombatShards ────────────────────────────────────────────────────────

describe("GrantCombatShards", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        // Default: not rate limited
        mockServer.GetUserInternalData.mockReturnValue({ Data: {} });
        mockServer.UpdateUserInternalData.mockReturnValue({});
        mockServer.GetUserData.mockReturnValue({ Data: {} });
        mockServer.UpdateUserData.mockReturnValue({});
        mockServer.GrantItemsToUser.mockReturnValue({});
    });

    test("returns INVALID_ARGS when heroId is missing", () => {
        const result = handlers.GrantCombatShards({ shardAmount: 3, battleToken: "tok123" }, {});
        expect(result.success).toBe(false);
        expect(result.error).toBe("INVALID_ARGS");
    });

    test("returns INVALID_ARGS when shardAmount is zero or negative", () => {
        const result = handlers.GrantCombatShards({ heroId: "hero_kaelen", shardAmount: 0, battleToken: "tok123" }, {});
        expect(result.success).toBe(false);
        expect(result.error).toBe("INVALID_ARGS");
    });

    test("returns INVALID_BATTLE_TOKEN when token not found in player data", () => {
        // validateAndConsumeBattleToken reads battle_tokens via GetUserData (not GetUserInternalData)
        mockServer.GetUserData.mockImplementation(({ Keys }) => {
            if (Keys && Keys.includes("battle_tokens")) {
                return { Data: { battle_tokens: { Value: JSON.stringify([]) } } };
            }
            return { Data: {} };
        });

        const result = handlers.GrantCombatShards({ heroId: "hero_kaelen", shardAmount: 5, battleToken: "invalid_tok" }, {});
        expect(result.success).toBe(false);
        expect(result.error).toBe("INVALID_BATTLE_TOKEN");
    });

    test("caps shards at 10 even if client sends more", () => {
        // Rate limiter uses GetUserInternalData; battle tokens use GetUserData via getPlayerDataValue
        mockServer.GetUserInternalData.mockReturnValue({ Data: {} });
        mockServer.GetUserData.mockImplementation(({ Keys }) => {
            if (Keys && Keys.includes("battle_tokens")) {
                return { Data: { battle_tokens: { Value: JSON.stringify(["valid_token"]) } } };
            }
            return { Data: {} };
        });

        const result = handlers.GrantCombatShards({ heroId: "hero_kaelen", shardAmount: 999, battleToken: "valid_token" }, {});
        expect(result.success).toBe(true);
        expect(result.totalShards).toBeLessThanOrEqual(10);
    });
});

// ─── ValidateIAP ─────────────────────────────────────────────────────────────

describe("ValidateIAP", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockServer.GetUserInternalData.mockReturnValue({ Data: {} });
        mockServer.UpdateUserInternalData.mockReturnValue({});
        mockServer.GetUserData.mockReturnValue({ Data: {} });
        mockServer.UpdateUserData.mockReturnValue({});
    });

    test("returns INVALID_ARGS when receiptData is missing", () => {
        const result = handlers.ValidateIAP({ platform: "ios", productId: "BATTLE_PASS_PREMIUM" }, {});
        expect(result.success).toBe(false);
        expect(result.error).toBe("INVALID_ARGS");
    });

    test("returns UNSUPPORTED_PLATFORM for unknown platform", () => {
        const result = handlers.ValidateIAP({ receiptData: "receipt", platform: "nintendo", productId: "BATTLE_PASS_PREMIUM" }, {});
        expect(result.success).toBe(false);
        expect(result.error).toBe("UNSUPPORTED_PLATFORM");
    });

    test("returns INVALID_RECEIPT when PlayFab validation fails", () => {
        mockServer.ValidateIOSReceipt.mockReturnValue({ Status: "Invalid" });
        const result = handlers.ValidateIAP({ receiptData: "bad_receipt", platform: "ios", productId: "BATTLE_PASS_PREMIUM" }, {});
        expect(result.success).toBe(false);
        expect(result.error).toBe("INVALID_RECEIPT");
    });

    test("prevents duplicate receipt processing", () => {
        const receipt = "valid_receipt_data";
        mockServer.ValidateIOSReceipt.mockReturnValue({ Status: "Valid" });
        // First call: no processed receipts
        mockServer.GetUserData.mockReturnValueOnce({ Data: {} });
        // Second call: receipt already in list
        mockServer.GetUserData.mockReturnValue({ Data: { processed_receipts: { Value: JSON.stringify(["-1684829004"]) } } });

        handlers.ValidateIAP({ receiptData: receipt, platform: "ios", productId: "BATTLE_PASS_PREMIUM" }, {});
        const result = handlers.ValidateIAP({ receiptData: receipt, platform: "ios", productId: "BATTLE_PASS_PREMIUM" }, {});
        expect(result.success).toBe(false);
        expect(result.error).toBe("DUPLICATE_RECEIPT");
    });
});

// ─── SubmitPvPOutcome ─────────────────────────────────────────────────────────

describe("SubmitPvPOutcome", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockServer.GetUserInternalData.mockReturnValue({ Data: {} });
        mockServer.UpdateUserInternalData.mockReturnValue({});
        mockServer.GetUserData.mockReturnValue({ Data: {} });
        mockServer.UpdateUserData.mockReturnValue({});
    });

    test("rejects invalid outcome values", () => {
        const result = handlers.SubmitPvPOutcome({
            attackerHeroIds: ["h1"],
            defenderPlayFabId: "DEF_001",
            outcome: "cheated",
            replayHash: "abc123",
            heroStatSnapshot: [{ heroId: "h1", attack: 100 }]
        }, {});
        expect(result.success).toBe(false);
        expect(result.error).toBe("INVALID_OUTCOME");
    });

    test("returns INVALID_ARGS when required fields are missing", () => {
        const result = handlers.SubmitPvPOutcome({ outcome: "win" }, {});
        expect(result.success).toBe(false);
        expect(result.error).toBe("INVALID_ARGS");
    });

    test("accepts valid win outcome and returns rewards", () => {
        const result = handlers.SubmitPvPOutcome({
            attackerHeroIds: ["hero_kaelen"],
            defenderPlayFabId: "DEF_001",
            outcome: "win",
            replayHash: "valid_hash_abc",
            heroStatSnapshot: [{ heroId: "hero_kaelen", attack: 120 }]
        }, {});
        expect(result.success).toBe(true);
        expect(result.rewards).toBeDefined();
        expect(result.rewards.xp).toBeGreaterThan(0);
    });
});

// ─── ApplyBuildSpeedup ────────────────────────────────────────────────────────

describe("ApplyBuildSpeedup", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockServer.GetUserInternalData.mockReturnValue({ Data: {} });
        mockServer.UpdateUserInternalData.mockReturnValue({});
        mockServer.ConsumeItem.mockReturnValue({});
    });

    test("returns INSUFFICIENT_ITEMS when player does not own speedup", () => {
        mockServer.GetUserInventory.mockReturnValue({ Inventory: [] });
        const result = handlers.ApplyBuildSpeedup({ placedBuildingId: "b1", speedupItemId: "speedup_1hr", quantity: 1 }, {});
        expect(result.success).toBe(false);
        expect(result.error).toBe("INSUFFICIENT_ITEMS");
    });

    test("returns correct seconds for 1hr speedup", () => {
        mockServer.GetUserInventory.mockReturnValue({
            Inventory: [{ ItemId: "speedup_1hr", ItemInstanceId: "inst_001" }]
        });
        const result = handlers.ApplyBuildSpeedup({ placedBuildingId: "b1", speedupItemId: "speedup_1hr", quantity: 1 }, {});
        expect(result.success).toBe(true);
        expect(result.secondsReduced).toBe(3600);
    });

    test("returns correct seconds for multiple 5min speedups", () => {
        mockServer.GetUserInventory.mockReturnValue({
            Inventory: [
                { ItemId: "speedup_5min", ItemInstanceId: "inst_001" },
                { ItemId: "speedup_5min", ItemInstanceId: "inst_002" },
                { ItemId: "speedup_5min", ItemInstanceId: "inst_003" }
            ]
        });
        const result = handlers.ApplyBuildSpeedup({ placedBuildingId: "b1", speedupItemId: "speedup_5min", quantity: 3 }, {});
        expect(result.success).toBe(true);
        expect(result.secondsReduced).toBe(900);
    });
});
