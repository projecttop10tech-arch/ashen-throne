#!/usr/bin/env node
/**
 * Runware AI Art Generator for Ashen Throne
 * Generates dark fantasy art assets for all game systems.
 *
 * Usage: node tools/generate-art.mjs [category]
 * Categories: heroes, cards, buildings, environments, ui, all
 */

import { randomUUID } from 'crypto';
import { writeFile, readFile, mkdir } from 'fs/promises';
import { existsSync } from 'fs';
import path from 'path';

const API_URL = 'https://api.runware.ai/v1';
const API_KEY = process.env.RUNWARE_API_KEY || 'mrkrkANAo0C2Cvi1w8UZqn95JK64ngJc';
const BASE_DIR = path.join(import.meta.dirname, '..', 'game', 'Assets', 'Art');
const MODEL = 'runware:100@1';

const STYLE_PREFIX = 'dark fantasy game art, painterly digital illustration, rich colors, dramatic lighting, ';
const EMPIRE_STYLE_PREFIX = 'mobile strategy game art, vibrant painterly digital illustration, bright saturated colors, warm golden daylight, colorful and inviting, ';
const STYLE_SUFFIX = ', high detail, professional game asset, transparent background where applicable';

// -------------------------------------------------------------------
// Asset definitions
// -------------------------------------------------------------------

const HEROES = [
    { id: 'kael_ashwalker', name: 'Kael Ashwalker', prompt: 'male dark elf fire mage with glowing ember eyes, wearing charred robes and ash-covered staff, flames swirling around hands' },
    { id: 'sera_dawnblade', name: 'Sera Dawnblade', prompt: 'female holy paladin in ornate golden plate armor, wielding a radiant longsword with divine light emanating from it' },
    { id: 'vex_shadowstrike', name: 'Vex Shadowstrike', prompt: 'male rogue assassin in dark leather armor, dual wielding shadow-infused daggers, shrouded in purple mist' },
    { id: 'lyra_thornveil', name: 'Lyra Thornveil', prompt: 'female nature druid with vine-wrapped staff, wild green hair, thorny armor made of living plants and flowers' },
    { id: 'thane_ironhold', name: 'Thane Ironhold', prompt: 'massive male dwarf warrior in heavy iron plate armor, wielding enormous warhammer, thick braided beard with metal clasps' },
    { id: 'nyx_stormcaller', name: 'Nyx Stormcaller', prompt: 'female storm sorceress floating in air, electric blue robes, lightning arcing between her fingers, stormy sky behind her' },
    { id: 'grim_bonecrusher', name: 'Grim Bonecrusher', prompt: 'undead death knight in skull-decorated black armor, wielding bone greatsword, green necromantic energy glowing from helmet visor' },
    { id: 'mira_frostbane', name: 'Mira Frostbane', prompt: 'female ice mage in crystalline blue robes, frost covering her arms, ice shards floating around her, pale blue skin' },
    { id: 'rowan_stoneward', name: 'Rowan Stoneward', prompt: 'male earth guardian covered in stone armor, glowing runes on his body, massive stone shield, mossy shoulders' },
    { id: 'zara_voidweaver', name: 'Zara Voidweaver', prompt: 'female void sorceress with swirling dark purple energy, torn astral robes, third eye on forehead, reality distorting around her' },
];

const CARD_ELEMENTS = [
    { id: 'Fire', prompt: 'ornate card frame border with flames and ember decorations, fire element, warm red and orange colors' },
    { id: 'Ice', prompt: 'ornate card frame border with frost crystals and snowflakes, ice element, cool blue and white colors' },
    { id: 'Lightning', prompt: 'ornate card frame border with electric arcs and storm clouds, lightning element, yellow and blue-white colors' },
    { id: 'Shadow', prompt: 'ornate card frame border with dark tendrils and skull motifs, shadow element, deep purple and black colors' },
    { id: 'Holy', prompt: 'ornate card frame border with divine light rays and angelic wings, holy element, golden and white colors' },
    { id: 'Nature', prompt: 'ornate card frame border with vines, leaves and flowers, nature element, green and brown earthy colors' },
    { id: 'Physical', prompt: 'ornate card frame border with metal rivets, chains and swords, physical element, silver and iron gray colors' },
    { id: 'Arcane', prompt: 'ornate card frame border with mystical runes and cosmic stars, arcane element, violet and teal colors' },
];

const BUILDINGS = [
    { id: 'stronghold', prompt: 'imposing medieval castle stronghold fortress with multiple towers, flying banners, heavy iron gates and glowing windows' },
    { id: 'barracks', prompt: 'military barracks with stone walls, weapon racks outside, training dummies, armored soldiers visible through windows' },
    { id: 'forge', prompt: 'blacksmith forge with roaring furnace glow, anvil, bellows, sparks flying, chimney billowing smoke and embers' },
    { id: 'marketplace', prompt: 'bustling marketplace with colorful merchant stalls, hanging lanterns, crates of goods, awnings and trade signs' },
    { id: 'academy', prompt: 'arcane academy tower with floating magical tomes, glowing windows, mystical sigils on walls, crystal spire' },
    { id: 'grain_farm', prompt: 'medieval grain farm with golden wheat fields, wooden barn, water mill, haystacks and a fenced yard' },
    { id: 'iron_mine', prompt: 'iron mine entrance carved into rock face, minecart tracks, timber supports, lanterns, ore piles outside' },
    { id: 'stone_quarry', prompt: 'stone quarry with carved blocks stacked high, wooden cranes, scaffolding, workers platforms' },
    { id: 'wall', prompt: 'fortified stone wall section with crenellations, mounted torches, arrow slits, iron reinforcements' },
    { id: 'watch_tower', prompt: 'tall stone watchtower with beacon fire at top, lookout platform, signal flags, spiral staircase visible' },
    { id: 'guild_hall', prompt: 'grand guild hall with carved stone facade, large wooden doors, heraldic banners, stained glass windows' },
    { id: 'embassy', prompt: 'diplomatic embassy with ornate columns, multiple nation flags, marble steps, decorative fountain' },
    { id: 'training_ground', prompt: 'open-air training ground with wooden practice dummies, sparring arena, weapon stands, sand pit' },
    { id: 'arcane_tower', prompt: 'tall magical spire with pulsing arcane crystals, floating rune rings, energy conduits, ethereal glow' },
    { id: 'hero_shrine', prompt: 'mystical hero shrine with glowing spirit stones, eternal flame braziers, carved hero statues, offerings' },
    { id: 'laboratory', prompt: 'alchemist laboratory with bubbling cauldrons, shelves of colorful potions, tubes and beakers, smoke vents' },
    { id: 'library', prompt: 'ancient grand library with towering bookshelves, floating candles, reading desks, enchanted book chains' },
    { id: 'armory', prompt: 'heavy stone armory with racks of gleaming swords and shields, armor stands, iron-bound doors' },
    { id: 'enchanting_tower', prompt: 'enchantment tower with glowing runic circles on floor, floating magical orbs, crystal conduits, purple energy' },
    { id: 'observatory', prompt: 'domed astronomical observatory with brass telescope, star charts, rotating celestial rings, skylight' },
    { id: 'archive', prompt: 'knowledge archive vault with endless scroll racks, magical record crystals, protective ward glyphs' },
];

const ENVIRONMENTS = [
    { id: 'tile_grass', prompt: 'top-down view of grass battlefield tile, dark fantasy, lush green' },
    { id: 'tile_stone', prompt: 'top-down view of stone floor battlefield tile, dark fantasy, cracked flagstone' },
    { id: 'tile_lava', prompt: 'top-down view of lava battlefield tile, dark fantasy, molten rock with cracks of fire' },
    { id: 'tile_ice', prompt: 'top-down view of frozen ice battlefield tile, dark fantasy, crystalline blue ice' },
    { id: 'tile_void', prompt: 'top-down view of void corruption battlefield tile, dark fantasy, purple dark energy tendrils' },
    { id: 'tile_holy', prompt: 'top-down view of sacred ground battlefield tile, dark fantasy, golden light runes' },
    { id: 'tile_swamp', prompt: 'top-down view of murky swamp battlefield tile, dark fantasy, dark water with reeds' },
    { id: 'worldmap_forest', prompt: 'bird eye view dark fantasy forest region for world map, dense trees, mist' },
    { id: 'worldmap_mountain', prompt: 'bird eye view dark fantasy mountain region for world map, snow peaks, dramatic' },
    { id: 'worldmap_desert', prompt: 'bird eye view dark fantasy desert wasteland for world map, sand dunes, ruins' },
    { id: 'worldmap_swamp', prompt: 'bird eye view dark fantasy swamp region for world map, murky water, dead trees' },
    { id: 'worldmap_volcanic', prompt: 'bird eye view dark fantasy volcanic region for world map, lava rivers, smoke' },
    { id: 'empire_bg', prompt: 'dark fantasy city overhead view background, medieval gothic architecture, bird eye perspective' },
    { id: 'empire_terrain_bg', prompt: 'vibrant fantasy medieval kingdom city map from above, bright isometric perspective mobile game art style, lush bright green grass terrain with gentle hills, many distinct cleared building plots arranged in a grid pattern connected by warm sandy stone pathways, bright daylight with warm golden sun, colorful flower beds and hedges between plots, tall green trees scattered around edges, small sparkling streams and ponds, stone walls with banners, large central plaza area for main castle, the terrain should be bright cheerful and colorful like a mobile strategy game city builder, no buildings just the terrain and paths and nature', w: 2048, h: 2048 },
];

const UI_ASSETS = [
    { id: 'app_icon', prompt: 'game app icon, dark fantasy throne made of swords with purple flames, simple iconic design', w: 1024, h: 1024 },
    { id: 'splash_screen', prompt: 'dark fantasy game splash screen, Ashen Throne text logo, burning throne, dramatic sky', w: 1088, h: 1920 },
    { id: 'btn_ornate', prompt: 'ornate dark fantasy UI button with gold filigree border, medieval style', w: 512, h: 192 },
    { id: 'panel_ornate', prompt: 'ornate dark fantasy UI panel background with stone texture and gold trim', w: 1024, h: 768 },
    { id: 'icon_currency_gems', prompt: 'game gem currency icon, glowing purple crystal, dark fantasy style', w: 256, h: 256 },
    { id: 'icon_currency_gold', prompt: 'game gold coin currency icon, ornate medieval coin, dark fantasy style', w: 256, h: 256 },
];

const EMPIRE_UI_ASSETS = [
    { id: 'resource_bar', prompt: 'horizontal ornate dark fantasy UI panel bar, gold filigree edges, stone texture center, medieval metalwork trim, seamless', w: 1920, h: 128 },
    { id: 'nav_bar', prompt: 'horizontal bottom navigation bar frame, dark gothic metalwork with gem inlays, ornate stone and iron, medieval UI panel', w: 1920, h: 192 },
    { id: 'building_panel', prompt: 'large dark fantasy UI modal panel, stone slab background with ornate gold trim border, medieval scroll details', w: 1024, h: 768 },
    { id: 'btn_primary', prompt: 'ornate golden action button, glowing warm edges, embossed medieval metalwork, rich gold and amber tones', w: 512, h: 128 },
    { id: 'btn_secondary', prompt: 'stone button with iron trim, subtle blue-gray glow, medieval carved texture, understated design', w: 512, h: 128 },
    { id: 'icon_gold', prompt: 'gold coin game icon, ornate medieval design with crown embossing, shiny metallic, warm golden tones', w: 256, h: 256 },
    { id: 'icon_gems', prompt: 'glowing purple crystal gem icon, faceted amethyst, magical inner glow, dark fantasy style', w: 256, h: 256 },
    { id: 'icon_stone', prompt: 'carved stone block resource icon, rough hewn granite with chisel marks, gray and brown tones', w: 256, h: 256 },
    { id: 'icon_iron', prompt: 'iron ingot resource icon, dark metallic sheen, heavy forged metal bar, cool steel tones', w: 256, h: 256 },
    { id: 'icon_grain', prompt: 'wheat sheaf bundle resource icon, golden ripe grain tied with twine, warm harvest tones', w: 256, h: 256 },
    { id: 'icon_arcane', prompt: 'swirling arcane essence orb icon, violet and teal magical energy, glowing mystical sphere', w: 256, h: 256 },
    { id: 'nav_empire', prompt: 'castle fortress navigation icon, simple iconic medieval keep silhouette, warm stone tones', w: 192, h: 192 },
    { id: 'nav_battle', prompt: 'crossed swords navigation icon, two medieval blades crossed, metallic silver and gold', w: 192, h: 192 },
    { id: 'nav_heroes', prompt: 'hero helmet navigation icon, ornate medieval knight helm with visor, dark iron with gold trim', w: 192, h: 192 },
    { id: 'nav_alliance', prompt: 'shield with banner navigation icon, heraldic shield with crossed flags, alliance symbol', w: 192, h: 192 },
    { id: 'nav_shop', prompt: 'treasure chest navigation icon, ornate wooden chest with gold clasps, slightly open with glow', w: 192, h: 192 },
];

// -------------------------------------------------------------------
// API helpers
// -------------------------------------------------------------------

async function generateImage(prompt, width, height, { stylePrefix = STYLE_PREFIX } = {}) {
    const taskUUID = randomUUID();
    const fullPrompt = stylePrefix + prompt + STYLE_SUFFIX;

    // Round to nearest multiple of 64
    width = Math.round(width / 64) * 64;
    height = Math.round(height / 64) * 64;

    const body = [{
        taskType: 'imageInference',
        taskUUID,
        positivePrompt: fullPrompt,
        negativePrompt: 'blurry, low quality, text, watermark, signature, ugly, deformed, amateur, pixelated',
        model: MODEL,
        width,
        height,
        outputFormat: 'PNG',
        numberResults: 1,
    }];

    const res = await fetch(API_URL, {
        method: 'POST',
        headers: {
            'Authorization': `Bearer ${API_KEY}`,
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(body),
    });

    if (!res.ok) {
        const text = await res.text();
        throw new Error(`Runware API error ${res.status}: ${text}`);
    }

    const data = await res.json();
    if (data.data && data.data.length > 0 && data.data[0].imageURL) {
        return data.data[0].imageURL;
    }
    throw new Error(`Unexpected response: ${JSON.stringify(data)}`);
}

async function removeBackground(imageURL) {
    const taskUUID = randomUUID();
    const body = [{
        taskType: 'imageBackgroundRemoval',
        taskUUID,
        inputImage: imageURL,
        outputFormat: 'PNG',
    }];

    const res = await fetch(API_URL, {
        method: 'POST',
        headers: {
            'Authorization': `Bearer ${API_KEY}`,
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(body),
    });

    if (!res.ok) {
        const text = await res.text();
        throw new Error(`Runware bg removal error ${res.status}: ${text}`);
    }

    const data = await res.json();
    if (data.data && data.data.length > 0 && data.data[0].imageURL) {
        return data.data[0].imageURL;
    }
    throw new Error(`BG removal unexpected response: ${JSON.stringify(data)}`);
}

async function downloadImage(url, outputPath) {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`Download failed: ${res.status}`);
    const buffer = Buffer.from(await res.arrayBuffer());
    await writeFile(outputPath, buffer);
    console.log(`  ✓ Saved: ${path.basename(outputPath)}`);
}

async function ensureDir(dir) {
    if (!existsSync(dir)) await mkdir(dir, { recursive: true });
}

// -------------------------------------------------------------------
// Generation functions
// -------------------------------------------------------------------

async function generateHeroes() {
    console.log('\n═══ Generating Hero Art ═══');
    const dir = path.join(BASE_DIR, 'Characters', 'Heroes');
    await ensureDir(dir);

    for (const hero of HEROES) {
        console.log(`\n  Hero: ${hero.name}`);
        try {
            // Portrait (512x512)
            const portraitUrl = await generateImage(
                `portrait bust shot of ${hero.prompt}, face and upper body, dark moody background`,
                512, 512
            );
            await downloadImage(portraitUrl, path.join(dir, `${hero.id}_portrait.png`));

            // Full body (512x1024)
            const fullUrl = await generateImage(
                `full body standing pose of ${hero.prompt}, full figure visible, dark fantasy environment`,
                512, 1024
            );
            await downloadImage(fullUrl, path.join(dir, `${hero.id}_fullbody.png`));

            // Small delay to respect rate limits
            await sleep(500);
        } catch (err) {
            console.error(`  ✗ Failed: ${hero.name} — ${err.message}`);
        }
    }
}

async function generateCards() {
    console.log('\n═══ Generating Card Frame Art ═══');
    const dir = path.join(BASE_DIR, 'UI', 'Cards');
    await ensureDir(dir);

    for (const card of CARD_ELEMENTS) {
        console.log(`  Card: ${card.id}`);
        try {
            const url = await generateImage(card.prompt, 384, 576);
            await downloadImage(url, path.join(dir, `CardFrame_${card.id}.png`));
            await sleep(500);
        } catch (err) {
            console.error(`  ✗ Failed: ${card.id} — ${err.message}`);
        }
    }
}

async function generateBuildings() {
    console.log('\n═══ Generating Building Art (bright + bg removal) ═══');
    const dir = path.join(BASE_DIR, 'Buildings');
    await ensureDir(dir);

    const tiers = [
        { suffix: 't1', desc: 'rustic simple wooden, few torches, basic construction,' },
        { suffix: 't2', desc: 'reinforced stone, glowing runes, banners, ornate details,' },
        { suffix: 't3', desc: 'grand towering, magical auras, gold accents, masterwork architecture,' },
    ];

    for (const building of BUILDINGS) {
        console.log(`\n  Building: ${building.id}`);
        for (const tier of tiers) {
            try {
                // Generate with bright style matching terrain
                const genUrl = await generateImage(
                    `${tier.desc} ${building.prompt}, bright colorful fantasy architecture, isometric game building sprite, top-down 3/4 view, vibrant saturated colors, warm sunlit, detailed textures, isolated single building on plain white background`,
                    768, 768,
                    { stylePrefix: EMPIRE_STYLE_PREFIX }
                );
                // Remove background for clean transparent sprite
                console.log(`    removing background...`);
                const cleanUrl = await removeBackground(genUrl);
                await downloadImage(cleanUrl, path.join(dir, `${building.id}_${tier.suffix}.png`));
                await sleep(400);
            } catch (err) {
                console.error(`  ✗ Failed: ${building.id}_${tier.suffix} — ${err.message}`);
            }
        }
    }
}

async function generateEnvironments() {
    console.log('\n═══ Generating Environment Art ═══');
    const dir = path.join(BASE_DIR, 'Environments');
    await ensureDir(dir);

    for (const env of ENVIRONMENTS) {
        console.log(`  Environment: ${env.id}`);
        try {
            const w = env.w || (env.id.startsWith('worldmap_') || env.id === 'empire_bg' ? 1024 : 512);
            const h = env.h || w;
            const url = await generateImage(env.prompt, w, h);
            await downloadImage(url, path.join(dir, `${env.id}.png`));
            await sleep(500);
        } catch (err) {
            console.error(`  ✗ Failed: ${env.id} — ${err.message}`);
        }
    }
}

async function generateUI() {
    console.log('\n═══ Generating UI Art ═══');
    const dir = path.join(BASE_DIR, 'UI', 'Production');
    await ensureDir(dir);

    for (const asset of UI_ASSETS) {
        console.log(`  UI: ${asset.id}`);
        try {
            const url = await generateImage(asset.prompt, asset.w, asset.h);
            await downloadImage(url, path.join(dir, `${asset.id}.png`));
            await sleep(500);
        } catch (err) {
            console.error(`  ✗ Failed: ${asset.id} — ${err.message}`);
        }
    }
}

async function generateEmpireTerrain() {
    console.log('\n═══ Generating Empire Terrain Background ═══');
    const dir = path.join(BASE_DIR, 'Environments');
    await ensureDir(dir);

    const terrain = ENVIRONMENTS.find(e => e.id === 'empire_terrain_bg');
    if (!terrain) { console.error('empire_terrain_bg not found in ENVIRONMENTS'); return; }

    console.log(`  Terrain: ${terrain.id} (${terrain.w}×${terrain.h})`);
    try {
        const url = await generateImage(
            terrain.prompt,
            terrain.w, terrain.h,
            { stylePrefix: EMPIRE_STYLE_PREFIX }
        );
        await downloadImage(url, path.join(dir, `${terrain.id}.png`));
    } catch (err) {
        console.error(`  ✗ Failed: ${terrain.id} — ${err.message}`);
    }
}

async function generateEmpireUI() {
    console.log('\n═══ Generating Empire UI Assets ═══');
    const dir = path.join(BASE_DIR, 'UI', 'Production');
    await ensureDir(dir);

    for (const asset of EMPIRE_UI_ASSETS) {
        console.log(`  UI: ${asset.id} (${asset.w}×${asset.h})`);
        try {
            const url = await generateImage(
                asset.prompt,
                asset.w, asset.h,
                { stylePrefix: EMPIRE_STYLE_PREFIX }
            );
            await downloadImage(url, path.join(dir, `${asset.id}.png`));
            await sleep(500);
        } catch (err) {
            console.error(`  ✗ Failed: ${asset.id} — ${err.message}`);
        }
    }
}

async function removeBuildingBackgrounds() {
    console.log('\n═══ Removing Backgrounds from Existing Buildings ═══');
    const dir = path.join(BASE_DIR, 'Buildings');

    const tiers = ['t1', 't2', 't3'];
    let processed = 0;

    for (const building of BUILDINGS) {
        for (const tier of tiers) {
            const filePath = path.join(dir, `${building.id}_${tier}.png`);
            if (!existsSync(filePath)) {
                console.log(`  ⊘ Skip (missing): ${building.id}_${tier}.png`);
                continue;
            }
            try {
                // Read file as base64
                const fileBuffer = await readFile(filePath);
                const base64 = `data:image/png;base64,${fileBuffer.toString('base64')}`;
                console.log(`  ${building.id}_${tier}: removing bg...`);
                const cleanUrl = await removeBackground(base64);
                await downloadImage(cleanUrl, filePath);
                processed++;
                await sleep(300);
            } catch (err) {
                console.error(`  ✗ Failed: ${building.id}_${tier} — ${err.message}`);
            }
        }
    }
    console.log(`\n  Processed ${processed} building sprites`);
}

function sleep(ms) {
    return new Promise(r => setTimeout(r, ms));
}

// -------------------------------------------------------------------
// Main
// -------------------------------------------------------------------

const category = process.argv[2] || 'all';
console.log(`\n🎨 Ashen Throne Art Generator — Category: ${category}`);
console.log(`   Using Runware AI (model: ${MODEL})\n`);

const start = Date.now();

try {
    switch (category) {
        case 'heroes': await generateHeroes(); break;
        case 'cards': await generateCards(); break;
        case 'buildings': await generateBuildings(); break;
        case 'environments': await generateEnvironments(); break;
        case 'ui': await generateUI(); break;
        case 'empire-terrain': await generateEmpireTerrain(); break;
        case 'empire-ui': await generateEmpireUI(); break;
        case 'buildings-remove-bg': await removeBuildingBackgrounds(); break;
        case 'empire':
            await generateEmpireTerrain();
            await generateBuildings();
            await generateEmpireUI();
            break;
        case 'all':
            await generateHeroes();
            await generateCards();
            await generateBuildings();
            await generateEnvironments();
            await generateUI();
            await generateEmpireTerrain();
            await generateEmpireUI();
            break;
        default:
            console.error(`Unknown category: ${category}`);
            console.log('Valid: heroes, cards, buildings, environments, ui, empire-terrain, empire-ui, empire, all');
            process.exit(1);
    }
} catch (err) {
    console.error(`\n✗ Fatal error: ${err.message}`);
}

const elapsed = ((Date.now() - start) / 1000).toFixed(1);
console.log(`\n✓ Done in ${elapsed}s`);
